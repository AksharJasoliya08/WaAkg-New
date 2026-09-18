using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.WaAkg.Services;
using Nop.Services.Events;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.Misc.WaAkg.Consumers;

/// <summary>
/// Queues the payment received message.
/// </summary>
public class OrderPaidConsumer : IConsumer<OrderPaidEvent>
{
    protected readonly ILogger _logger;
    protected readonly IWaAkgQueueService _queueService;
    protected readonly IWaAkgTokenService _tokenService;
    protected readonly IWaAkgErrorLogService _errorLogService;
    protected readonly WaAkgSettings _settings;

    public OrderPaidConsumer(ILogger logger,
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
    public async Task HandleEventAsync(OrderPaidEvent eventMessage)
    {
        try
        {
            var order = eventMessage?.Order;
            if (order == null || !_settings.Enabled)
                return;

            if (_settings.EnableOrderPaid && await _queueService.IsOptedInAsync(order.CustomerId))
            {
                var phone = await _tokenService.GetOrderPhoneAsync(order);
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    var message = await _tokenService.BuildForOrderAsync(_settings.TemplateOrderPaid, order);
                    await _queueService.EnqueueAsync(phone, message, WaAkgDefaults.Events.OrderPaid,
                        orderId: order.Id, customerId: order.CustomerId);
                }
            }

            await SendOwnerCopyAsync(order);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("WA-AKG: OrderPaidConsumer failed", ex);
            await _errorLogService.LogAsync("OrderPaidConsumer", ex.Message, ex.ToString());
        }
    }

    /// <summary>Let the owner know a COD advance / full payment just came in.</summary>
    protected virtual async Task SendOwnerCopyAsync(Order order)
    {
        if (!_settings.OwnerNotificationsEnabled)
            return;

        var numbers = (_settings.OwnerNotificationNumbers ?? string.Empty)
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct()
            .ToList();

        if (!numbers.Any())
            return;

        var message = await _tokenService.BuildForOrderAsync(_settings.TemplateOwnerCod, order);

        foreach (var number in numbers)
            await _queueService.EnqueueAsync(number, message, WaAkgDefaults.Events.OwnerNotification,
                orderId: order.Id, customerId: order.CustomerId);
    }
}
