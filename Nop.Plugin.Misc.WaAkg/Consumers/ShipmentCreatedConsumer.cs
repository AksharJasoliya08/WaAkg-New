using Nop.Core.Domain.Shipping;
using Nop.Core.Events;
using Nop.Plugin.Misc.WaAkg.Services;
using Nop.Services.Events;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.Misc.WaAkg.Consumers;

/// <summary>
/// Queues the "shipped" message with the tracking number.
/// </summary>
public class ShipmentCreatedConsumer : IConsumer<EntityInsertedEvent<Shipment>>
{
    protected readonly ILogger _logger;
    protected readonly IOrderService _orderService;
    protected readonly IWaAkgQueueService _queueService;
    protected readonly IWaAkgTokenService _tokenService;
    protected readonly IWaAkgErrorLogService _errorLogService;
    protected readonly WaAkgSettings _settings;

    public ShipmentCreatedConsumer(ILogger logger,
        IOrderService orderService,
        IWaAkgQueueService queueService,
        IWaAkgTokenService tokenService,
        IWaAkgErrorLogService errorLogService,
        WaAkgSettings settings)
    {
        _logger = logger;
        _orderService = orderService;
        _queueService = queueService;
        _tokenService = tokenService;
        _errorLogService = errorLogService;
        _settings = settings;
    }

    /// <summary>Handle the event.</summary>
    public async Task HandleEventAsync(EntityInsertedEvent<Shipment> eventMessage)
    {
        try
        {
            var shipment = eventMessage?.Entity;
            if (shipment == null || !_settings.Enabled || !_settings.EnableShipment)
                return;

            var order = await _orderService.GetOrderByIdAsync(shipment.OrderId);
            if (order == null)
                return;

            if (!await _queueService.IsOptedInAsync(order.CustomerId))
                return;

            var phone = await _tokenService.GetOrderPhoneAsync(order);
            if (string.IsNullOrWhiteSpace(phone))
                return;

            var message = await _tokenService.BuildForOrderAsync(_settings.TemplateShipment, order, shipment);
            await _queueService.EnqueueAsync(phone, message, WaAkgDefaults.Events.Shipment,
                orderId: order.Id, customerId: order.CustomerId);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("WA-AKG: ShipmentCreatedConsumer failed", ex);
            await _errorLogService.LogAsync("ShipmentCreatedConsumer", ex.Message, ex.ToString());
        }
    }
}
