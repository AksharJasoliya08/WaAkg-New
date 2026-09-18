using LinqToDB;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.Misc.WaAkg.Services;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Misc.WaAkg.Tasks;

/// <summary>
/// Queues an abandoned cart reminder for opted-in customers whose cart has been
/// idle longer than the configured window and who have not ordered since.
/// </summary>
public class CartReminderTask : IScheduleTask
{
    #region Fields

    protected readonly ICustomerService _customerService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly ILogger _logger;
    protected readonly IRepository<Order> _orderRepository;
    protected readonly IRepository<ShoppingCartItem> _cartRepository;
    protected readonly IWaAkgQueueService _queueService;
    protected readonly IWaAkgTokenService _tokenService;
    protected readonly IWaAkgErrorLogService _errorLogService;
    protected readonly WaAkgSettings _settings;

    #endregion

    #region Ctor

    public CartReminderTask(ICustomerService customerService,
        IGenericAttributeService genericAttributeService,
        ILogger logger,
        IRepository<Order> orderRepository,
        IRepository<ShoppingCartItem> cartRepository,
        IWaAkgQueueService queueService,
        IWaAkgTokenService tokenService,
        IWaAkgErrorLogService errorLogService,
        WaAkgSettings settings)
    {
        _customerService = customerService;
        _genericAttributeService = genericAttributeService;
        _logger = logger;
        _orderRepository = orderRepository;
        _cartRepository = cartRepository;
        _queueService = queueService;
        _tokenService = tokenService;
        _errorLogService = errorLogService;
        _settings = settings;
    }

    #endregion

    #region Methods

    /// <summary>Execute the task.</summary>
    public async Task ExecuteAsync()
    {
        if (!_settings.Enabled || !_settings.EnableCartReminder)
            return;

        var now = DateTime.UtcNow;
        var idleSince = now.AddHours(-Math.Max(1, _settings.CartReminderHours));

        // customers with a shopping cart untouched for longer than the window
        var candidateIds = await _cartRepository.Table
            .Where(item => item.ShoppingCartTypeId == (int)ShoppingCartType.ShoppingCart
                           && item.UpdatedOnUtc < idleSince)
            .Select(item => item.CustomerId)
            .Distinct()
            .Take(500)
            .ToListAsync();

        if (!candidateIds.Any())
            return;

        // anyone who ordered inside the window no longer needs a reminder
        var recentBuyers = await _orderRepository.Table
            .Where(order => !order.Deleted && order.CreatedOnUtc >= idleSince && candidateIds.Contains(order.CustomerId))
            .Select(order => order.CustomerId)
            .Distinct()
            .ToListAsync();

        var queued = 0;

        foreach (var customerId in candidateIds.Except(recentBuyers))
        {
            try
            {
                if (!await _queueService.IsOptedInAsync(customerId))
                    continue;

                var customer = await _customerService.GetCustomerByIdAsync(customerId);
                if (customer == null || await _customerService.IsGuestAsync(customer))
                    continue;

                // never remind the same customer more than once a week
                var lastReminder = await _genericAttributeService
                    .GetAttributeAsync<DateTime?>(customer, WaAkgDefaults.LastCartReminderAttribute);
                if (lastReminder.HasValue && lastReminder.Value > now.AddDays(-7))
                    continue;

                var phone = await _tokenService.GetCustomerPhoneAsync(customer);
                if (string.IsNullOrWhiteSpace(phone))
                    continue;

                // one message per cart item, each with that item's own picture as its caption -
                // WhatsApp has no multi-image single message, so this is the only way to show
                // every item's picture rather than just the first one
                var items = await _tokenService.GetCartItemsInfoAsync(customer);
                if (!items.Any())
                    continue;

                foreach (var item in items)
                {
                    var itemMessage = $"{item.Quantity} x {item.ProductName} - {item.PriceText}" +
                        (string.IsNullOrWhiteSpace(item.ProductUrl) ? string.Empty : $"\n{item.ProductUrl}");

                    if (!string.IsNullOrWhiteSpace(item.ImageUrl))
                        await _queueService.EnqueueAsync(phone, itemMessage, WaAkgDefaults.Events.CartReminder,
                            item.ImageUrl, item.ImageMimeType, customerId: customerId);
                    else
                        await _queueService.EnqueueAsync(phone, itemMessage, WaAkgDefaults.Events.CartReminder,
                            customerId: customerId);
                }

                // closing message with the coupon and a link back to the cart
                var message = await _tokenService.BuildForCustomerAsync(_settings.TemplateCartReminder, customer,
                    new Dictionary<string, string> { ["%CouponCode%"] = _settings.CartReminderCoupon });

                await _queueService.EnqueueAsync(phone, message, WaAkgDefaults.Events.CartReminder,
                    customerId: customerId);

                await _genericAttributeService.SaveAttributeAsync(customer,
                    WaAkgDefaults.LastCartReminderAttribute, now);

                queued++;
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"WA-AKG: cart reminder failed for customer {customerId}", ex);
                await _errorLogService.LogAsync("CartReminderTask", ex.Message, ex.ToString());
            }
        }

        if (queued > 0)
            await _logger.InformationAsync($"WA-AKG: {queued} cart reminder(s) queued.");
    }

    #endregion
}
