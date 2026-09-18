using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Seo;

namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Default token replacement implementation.
/// </summary>
public class WaAkgTokenService : IWaAkgTokenService
{
    #region Fields

    protected readonly IAddressService _addressService;
    protected readonly ICustomerService _customerService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderService _orderService;
    protected readonly IPictureService _pictureService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IPriceCalculationService _priceCalculationService;
    protected readonly IProductService _productService;
    protected readonly IShoppingCartService _shoppingCartService;
    protected readonly IStoreContext _storeContext;
    protected readonly IUrlRecordService _urlRecordService;
    protected readonly ICodBridgeService _codBridgeService;
    protected readonly WaAkgSettings _settings;

    #endregion

    #region Ctor

    public WaAkgTokenService(IAddressService addressService,
        ICustomerService customerService,
        ILocalizationService localizationService,
        IOrderService orderService,
        IPictureService pictureService,
        IPriceFormatter priceFormatter,
        IPriceCalculationService priceCalculationService,
        IProductService productService,
        IShoppingCartService shoppingCartService,
        IStoreContext storeContext,
        IUrlRecordService urlRecordService,
        ICodBridgeService codBridgeService,
        WaAkgSettings settings)
    {
        _addressService = addressService;
        _customerService = customerService;
        _localizationService = localizationService;
        _orderService = orderService;
        _pictureService = pictureService;
        _priceFormatter = priceFormatter;
        _priceCalculationService = priceCalculationService;
        _productService = productService;
        _shoppingCartService = shoppingCartService;
        _storeContext = storeContext;
        _urlRecordService = urlRecordService;
        _codBridgeService = codBridgeService;
        _settings = settings;
    }

    #endregion

    #region Utilities

    /// <summary>Apply a dictionary of replacements to a template.</summary>
    protected virtual string Replace(string template, IDictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        foreach (var token in tokens)
            template = template.Replace(token.Key, token.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        return template;
    }

    /// <summary>Tokens that are always available.</summary>
    protected virtual async Task<Dictionary<string, string>> GetCommonTokensAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        return new Dictionary<string, string>
        {
            ["%StoreName%"] = store?.Name,
            ["%StoreUrl%"] = store?.Url?.TrimEnd('/'),
            ["%CartUrl%"] = $"{store?.Url?.TrimEnd('/')}/cart",
            ["%CouponCode%"] = _settings.CartReminderCoupon,
            ["%PaymentLink%"] = _settings.PaymentLink,
            ["%AdvancePaid%"] = string.Empty,
            ["%CodDue%"] = string.Empty,
            ["%CodSummary%"] = string.Empty,
            ["%PaymentMethod%"] = string.Empty,
            ["%AdminOrderUrl%"] = string.Empty,
            ["%OrderItems%"] = string.Empty,
            ["%CartItems%"] = string.Empty,
            ["%TrackUrl%"] = string.Empty,
            ["%ProductUrl%"] = string.Empty
        };
    }

    /// <summary>Public absolute URL of a picture, or null.
    /// storeLocation is passed explicitly because this runs from a background scheduled task
    /// (no HttpContext), where GetPictureUrlAsync cannot otherwise infer the store's own domain
    /// and would return a relative path the gateway can never fetch.</summary>
    protected virtual async Task<string> GetPictureUrlAsync(int pictureId)
    {
        if (pictureId <= 0)
            return null;

        var store = await _storeContext.GetCurrentStoreAsync();
        var storeLocation = store?.Url?.TrimEnd('/') + "/";

        var url = await _pictureService.GetPictureUrlAsync(pictureId, showDefaultPicture: false,
            storeLocation: storeLocation);

        return string.IsNullOrWhiteSpace(url) ? null : url;
    }

    /// <summary>Storefront URL of a product's own page, e.g. https://store/product-seo-name.</summary>
    protected virtual async Task<string> GetProductUrlAsync(Nop.Core.Domain.Catalog.Product product)
    {
        if (product == null)
            return null;

        var store = await _storeContext.GetCurrentStoreAsync();
        var seName = await _urlRecordService.GetSeNameAsync(product);

        return string.IsNullOrWhiteSpace(seName)
            ? $"{store?.Url?.TrimEnd('/')}/{product.Id}"
            : $"{store?.Url?.TrimEnd('/')}/{seName}";
    }

    #endregion

    #region Methods

    /// <summary>Tokens available to the admin template editor.</summary>
    public virtual IList<string> GetAllowedTokens() => new List<string>
    {
        "%CustomerName%", "%CustomerPhone%", "%OrderNumber%", "%Total%",
        "%PaymentStatus%", "%ShippingStatus%", "%Carrier%", "%TrackingNumber%",
        "%StoreName%", "%StoreUrl%", "%CartUrl%", "%CouponCode%", "%PaymentLink%",
        "%OrderItems%", "%CartItems%", "%AdvancePaid%", "%CodDue%", "%CodSummary%",
        "%PaymentMethod%", "%AdminOrderUrl%", "%TrackUrl%", "%ProductUrl%"
    };

    /// <summary>Build a message for an order related event.</summary>
    public virtual async Task<string> BuildForOrderAsync(string template, Order order, Shipment shipment = null)
    {
        if (order == null)
            return template;

        var tokens = await GetCommonTokensAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        var billing = await _addressService.GetAddressByIdAsync(order.BillingAddressId);
        var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);

        var name = billing?.FirstName;
        if (string.IsNullOrWhiteSpace(name))
            name = customer == null ? string.Empty : await _customerService.GetCustomerFullNameAsync(customer);
        if (string.IsNullOrWhiteSpace(name))
            name = "there";

        var orderNumber = string.IsNullOrEmpty(order.CustomOrderNumber) ? order.Id.ToString() : order.CustomOrderNumber;

        tokens["%CustomerName%"] = name.Trim();
        tokens["%CustomerPhone%"] = await GetOrderPhoneAsync(order);
        tokens["%OrderNumber%"] = orderNumber;
        tokens["%Total%"] = await _priceFormatter.FormatPriceAsync(order.OrderTotal, true,
            order.CustomerCurrencyCode, false, store?.DefaultLanguageId ?? 0);
        tokens["%PaymentStatus%"] = await _localizationService.GetLocalizedEnumAsync(order.PaymentStatus);
        tokens["%ShippingStatus%"] = await _localizationService.GetLocalizedEnumAsync(order.ShippingStatus);
        tokens["%PaymentMethod%"] = order.PaymentMethodSystemName;
        tokens["%AdminOrderUrl%"] = $"{store?.Url?.TrimEnd('/')}/Admin/Order/Edit/{order.Id}";
        tokens["%OrderItems%"] = await BuildOrderItemsListAsync(order);
        tokens["%TrackUrl%"] = await GetOrderTrackUrlAsync(order);
        tokens["%ProductUrl%"] = await GetOrderProductUrlAsync(order);

        var carrier = order.ShippingMethod;
        var tracking = string.Empty;

        if (shipment != null)
        {
            tracking = shipment.TrackingNumber;
            if (string.IsNullOrWhiteSpace(carrier))
                carrier = order.ShippingRateComputationMethodSystemName;
        }

        tokens["%Carrier%"] = carrier;
        tokens["%TrackingNumber%"] = string.IsNullOrWhiteSpace(tracking) ? "-" : tracking;

        // COD breakdown, when the Advanced COD plugin is installed and has a record for this order
        var codInfo = await _codBridgeService.GetOrderCodInfoAsync(order.Id);
        if (codInfo != null)
        {
            var languageId = store?.DefaultLanguageId ?? 0;
            var advancePaidText = await _priceFormatter.FormatPriceAsync(codInfo.AdvancePaid, true, order.CustomerCurrencyCode, false, languageId);
            var codDueText = await _priceFormatter.FormatPriceAsync(codInfo.CodDue, true, order.CustomerCurrencyCode, false, languageId);

            tokens["%AdvancePaid%"] = advancePaidText;
            tokens["%CodDue%"] = codDueText;
            tokens["%CodSummary%"] = codInfo.CodDue > 0
                ? $"Paid: {advancePaidText} | COD due on delivery: {codDueText}"
                : $"Paid in full: {advancePaidText}";
        }

        return Replace(template, tokens);
    }

    /// <summary>Build a message for a customer related event.</summary>
    public virtual async Task<string> BuildForCustomerAsync(string template, Customer customer,
        IDictionary<string, string> extra = null)
    {
        var tokens = await GetCommonTokensAsync();

        var name = customer == null ? string.Empty : await _customerService.GetCustomerFullNameAsync(customer);
        if (string.IsNullOrWhiteSpace(name))
            name = "there";

        tokens["%CustomerName%"] = name.Trim();
        tokens["%CustomerPhone%"] = await GetCustomerPhoneAsync(customer);
        tokens["%OrderNumber%"] = string.Empty;
        tokens["%Total%"] = string.Empty;
        tokens["%PaymentStatus%"] = string.Empty;
        tokens["%ShippingStatus%"] = string.Empty;
        tokens["%Carrier%"] = string.Empty;
        tokens["%TrackingNumber%"] = string.Empty;
        tokens["%CartItems%"] = customer == null ? string.Empty : await BuildCartItemsListAsync(customer);

        if (extra != null)
            foreach (var pair in extra)
                tokens[pair.Key] = pair.Value;

        return Replace(template, tokens);
    }

    /// <summary>Resolve the best WhatsApp number for an order.</summary>
    public virtual async Task<string> GetOrderPhoneAsync(Order order)
    {
        if (order == null)
            return null;

        var billing = await _addressService.GetAddressByIdAsync(order.BillingAddressId);
        if (!string.IsNullOrWhiteSpace(billing?.PhoneNumber))
            return billing.PhoneNumber;

        if (order.ShippingAddressId.HasValue)
        {
            var shipping = await _addressService.GetAddressByIdAsync(order.ShippingAddressId.Value);
            if (!string.IsNullOrWhiteSpace(shipping?.PhoneNumber))
                return shipping.PhoneNumber;
        }

        var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);
        return await GetCustomerPhoneAsync(customer);
    }

    /// <summary>Resolve the best WhatsApp number for a customer.</summary>
    public virtual async Task<string> GetCustomerPhoneAsync(Customer customer)
    {
        if (customer == null)
            return null;

        if (!string.IsNullOrWhiteSpace(customer.Phone))
            return customer.Phone;

        var addresses = await _customerService.GetAddressesByCustomerIdAsync(customer.Id);
        return addresses?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.PhoneNumber))?.PhoneNumber;
    }

    /// <summary>Public, absolute URL of an order item's product picture, and its real MIME type,
    /// or (null, null) if it has none.</summary>
    public virtual async Task<(string url, string mimeType)> GetOrderMainImageUrlAsync(Order order)
    {
        if (order == null)
            return (null, null);

        var items = await _orderService.GetOrderItemsAsync(order.Id);
        var firstItem = items?.FirstOrDefault();
        if (firstItem == null)
            return (null, null);

        var product = await _productService.GetProductByIdAsync(firstItem.ProductId);
        if (product == null)
            return (null, null);

        return await GetProductMainImageAsync(product.Id);
    }

    /// <summary>Public, absolute URL of the first item in a customer's current cart, and its real
    /// MIME type, or (null, null) if there is none.</summary>
    public virtual async Task<(string url, string mimeType)> GetCartMainImageUrlAsync(Customer customer)
    {
        if (customer == null)
            return (null, null);

        var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart);
        var firstItem = cart?.FirstOrDefault();
        if (firstItem == null)
            return (null, null);

        return await GetProductMainImageAsync(firstItem.ProductId);
    }

    /// <summary>Shared lookup: a product's first picture, as a public URL plus its actual MIME type.</summary>
    protected virtual async Task<(string url, string mimeType)> GetProductMainImageAsync(int productId)
    {
        var pictures = await _productService.GetProductPicturesByProductIdAsync(productId);
        var pictureId = pictures?.FirstOrDefault()?.PictureId ?? 0;

        if (pictureId <= 0)
            return (null, null);

        var picture = await _pictureService.GetPictureByIdAsync(pictureId);
        var url = await GetPictureUrlAsync(pictureId);

        var mimeType = string.IsNullOrWhiteSpace(picture?.MimeType) ? "image/jpeg" : picture.MimeType;

        return (url, mimeType);
    }

    /// <summary>Customer-facing order details page URL (%TrackUrl%), built from the configured pattern.</summary>
    public virtual async Task<string> GetOrderTrackUrlAsync(Order order)
    {
        if (order == null)
            return null;

        var store = await _storeContext.GetCurrentStoreAsync();
        var storeUrl = store?.Url?.TrimEnd('/') + "/";
        var pattern = string.IsNullOrWhiteSpace(_settings.OrderTrackUrlPattern)
            ? "{0}orderdetails/{1}"
            : _settings.OrderTrackUrlPattern;

        try
        {
            return string.Format(pattern, storeUrl, order.Id);
        }
        catch (FormatException)
        {
            // a malformed pattern in settings should not break every message - fall back to the default
            return $"{storeUrl}orderdetails/{order.Id}";
        }
    }

    /// <summary>Public storefront URL of the product page for the first item in an order, or null.</summary>
    public virtual async Task<string> GetOrderProductUrlAsync(Order order)
    {
        if (order == null)
            return null;

        var items = await _orderService.GetOrderItemsAsync(order.Id);
        var firstItem = items?.FirstOrDefault();
        if (firstItem == null)
            return null;

        var product = await _productService.GetProductByIdAsync(firstItem.ProductId);
        return await GetProductUrlAsync(product);
    }

    /// <summary>Every item currently in the customer's cart, each with its own image, name and
    /// formatted unit price - so the caller can send one WhatsApp message per item.</summary>
    public virtual async Task<IList<CartItemInfo>> GetCartItemsInfoAsync(Customer customer)
    {
        var result = new List<CartItemInfo>();

        if (customer == null)
            return result;

        var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart);
        if (cart == null || !cart.Any())
            return result;

        var languageId = (await _storeContext.GetCurrentStoreAsync())?.DefaultLanguageId ?? 0;

        foreach (var item in cart)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            if (product == null)
                continue;

            var (imageUrl, imageMimeType) = await GetProductMainImageAsync(product.Id);
            var priceText = await _priceFormatter.FormatPriceAsync(product.Price, true, string.Empty, false, languageId);

            result.Add(new CartItemInfo
            {
                ProductName = product.Name,
                PriceText = priceText,
                Quantity = item.Quantity,
                ImageUrl = imageUrl,
                ImageMimeType = imageMimeType,
                ProductUrl = await GetProductUrlAsync(product)
            });
        }

        return result;
    }

    /// <summary>One line per order item: "2 x Necklace Name - ₹499".</summary>
    public virtual async Task<string> BuildOrderItemsListAsync(Order order)
    {
        if (order == null)
            return string.Empty;

        var items = await _orderService.GetOrderItemsAsync(order.Id);
        if (items == null || !items.Any())
            return string.Empty;

        var languageId = (await _storeContext.GetCurrentStoreAsync())?.DefaultLanguageId ?? 0;
        var lines = new List<string>();

        foreach (var item in items)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            var name = product?.Name ?? "Item";
            var priceText = await _priceFormatter.FormatPriceAsync(item.PriceInclTax, true,
                order.CustomerCurrencyCode, false, languageId);

            lines.Add($"{item.Quantity} x {name} - {priceText}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>One line per cart item, same format as <see cref="BuildOrderItemsListAsync"/>.</summary>
    public virtual async Task<string> BuildCartItemsListAsync(Customer customer)
    {
        if (customer == null)
            return string.Empty;

        var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart);
        if (cart == null || !cart.Any())
            return string.Empty;

        var lines = new List<string>();

        foreach (var item in cart)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            if (product == null)
                continue;

            var languageId = (await _storeContext.GetCurrentStoreAsync())?.DefaultLanguageId ?? 0;
            var priceText = await _priceFormatter.FormatPriceAsync(product.Price, true, string.Empty, false, languageId);

            lines.Add($"{item.Quantity} x {product.Name} - {priceText}");
        }

        return string.Join("\n", lines);
    }

    #endregion
}
