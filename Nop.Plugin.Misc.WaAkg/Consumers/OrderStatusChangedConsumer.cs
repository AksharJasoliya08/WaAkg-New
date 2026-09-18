using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Events;
using Nop.Plugin.Misc.WaAkg.Services;
using Nop.Services.Events;
using Nop.Services.Logging;

namespace Nop.Plugin.Misc.WaAkg.Consumers;

/// <summary>
/// Maps order status transitions to delivered / cancelled messages.
/// A queue row per (order, event) is written at most once.
/// </summary>
public class OrderStatusChangedConsumer : IConsumer<EntityUpdatedEvent<Order>>
{
    protected readonly ILogger _logger;
    protected readonly IWaAkgQueueService _queueService;
    protected readonly IWaAkgTokenService _tokenService;
    protected readonly IWaAkgErrorLogService _errorLogService;
    protected readonly WaAkgSettings _settings;

    public OrderStatusChangedConsumer(ILogger logger,
        IWaAkgQueueService queueService,
        IWaAkgTokenService tokenService,
        IWaAkgErrorLogService errorLogService,
        WaAkgSettings settings)
    {
        _logger = logger;
        _queueService = queueService;
        _tokenService = tokenService;
        _errorLogService = errorLogService;
        _settings = settings;
    }

    /// <summary>Handle the event.</summary>
    public async Task HandleEventAsync(EntityUpdatedEvent<Order> eventMessage)
    {
        try
        {
            var order = eventMessage?.Entity;
            if (order == null || !_settings.Enabled)
                return;

            string template;
            string eventName;

            if (order.OrderStatus == OrderStatus.Complete && _settings.EnableDelivered)
            {
                template = _settings.TemplateDelivered;
                eventName = WaAkgDefaults.Events.Delivered;
            }
            else if ((order.OrderStatus == OrderStatus.Cancelled ||
                      order.PaymentStatus == PaymentStatus.Refunded ||
                      order.PaymentStatus == PaymentStatus.PartiallyRefunded) && _settings.EnableCancelled)
            {
                template = _settings.TemplateCancelled;
                eventName = WaAkgDefaults.Events.Cancelled;
            }
            else
            {
                return;
            }

            if (!await _queueService.IsOptedInAsync(order.CustomerId))
                return;

            // de-duplicate: the order entity is updated many times during its life
            var existing = await _queueService.SearchAsync(eventName, 0, 0, 500);
            if (existing.Any(m => m.OrderId == order.Id))
                return;

            var phone = await _tokenService.GetOrderPhoneAsync(order);
            if (string.IsNullOrWhiteSpace(phone))
                return;

            var message = await _tokenService.BuildForOrderAsync(template, order);
            await _queueService.EnqueueAsync(phone, message, eventName,
                orderId: order.Id, customerId: order.CustomerId);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("WA-AKG: OrderStatusChangedConsumer failed", ex);
            await _errorLogService.LogAsync("OrderStatusChangedConsumer", ex.Message, ex.ToString());
        }
    }
}
