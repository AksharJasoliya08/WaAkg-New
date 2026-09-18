using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;

namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Replaces %Token% placeholders inside message templates.
/// </summary>
public interface IWaAkgTokenService
{
    /// <summary>Tokens available to the admin template editor.</summary>
    IList<string> GetAllowedTokens();

    /// <summary>Build a message for an order related event.</summary>
    Task<string> BuildForOrderAsync(string template, Order order, Shipment shipment = null);

    /// <summary>Build a message for a customer related event.</summary>
    Task<string> BuildForCustomerAsync(string template, Customer customer, IDictionary<string, string> extra = null);

    /// <summary>Resolve the best WhatsApp number for an order.</summary>
    Task<string> GetOrderPhoneAsync(Order order);

    /// <summary>Resolve the best WhatsApp number for a customer.</summary>
    Task<string> GetCustomerPhoneAsync(Customer customer);

    /// <summary>Public, absolute URL of an order item's product picture and its real MIME type,
    /// or (null, null) if it has none.</summary>
    Task<(string url, string mimeType)> GetOrderMainImageUrlAsync(Order order);

    /// <summary>Public, absolute URL of the first item in a customer's current cart and its real
    /// MIME type, or (null, null) if there is none.</summary>
    Task<(string url, string mimeType)> GetCartMainImageUrlAsync(Customer customer);

    /// <summary>Every item currently in the customer's cart, each with its own image URL/MIME
    /// (when it has a picture), name and formatted unit price - one entry per item, so the
    /// caller can send one WhatsApp message per item instead of a single combined message.</summary>
    Task<IList<CartItemInfo>> GetCartItemsInfoAsync(Customer customer);

    /// <summary>Public storefront URL of the product page for the first item in an order, or null.</summary>
    Task<string> GetOrderProductUrlAsync(Order order);

    /// <summary>Customer-facing order details page URL (%TrackUrl%), built from the configured pattern.</summary>
    Task<string> GetOrderTrackUrlAsync(Order order);

    /// <summary>One line per order item: "2 x Necklace Name - ₹499".</summary>
    Task<string> BuildOrderItemsListAsync(Order order);

    /// <summary>One line per cart item, same format as <see cref="BuildOrderItemsListAsync"/>.</summary>
    Task<string> BuildCartItemsListAsync(Customer customer);
}

/// <summary>One shopping cart item's display info, used to send one message per item.</summary>
public class CartItemInfo
{
    public string ProductName { get; set; }
    public string PriceText { get; set; }
    public int Quantity { get; set; }
    public string ImageUrl { get; set; }
    public string ImageMimeType { get; set; }
    public string ProductUrl { get; set; }
}

