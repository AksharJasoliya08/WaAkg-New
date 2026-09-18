using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.WaAkg.Services;
using Nop.Services.Events;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.Misc.WaAkg.Consumers;

/// <summary>
/// Queues a single order confirmation message (product image with the full order text as its
/// caption, or plain text if the product has no image) when an order is placed, a COD advance
/// nudge for COD orders (auto-cancelled if the customer pays before it sends - see
/// CodAdvancePaidConsumer), a COD due summary, and a copy of the same event to the store owner
/// if enabled.
/// </summary>
public class OrderPlacedConsumer : IConsumer<OrderPlacedEvent>
{
    protected readonly ILogger _logger;
    protected readonly IWaAkgQueueService _queueService;
    protected readonly IWaAkgTokenService _tokenService;
    protected readonly IWaAkgErrorLogService _errorLogService;
    protected readonly WaAkgSettings _settings;

    public OrderPlacedConsumer(ILogger logger,
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
    public async Task HandleEventAsync(OrderPlacedEvent eventMessage)
    {
        try
        {
            var order = eventMessage?.Order;
            if (order == null || !_settings.Enabled)
                return;

            var isCod = (order.PaymentMethodSystemName ?? string.Empty)
                .Contains("CashOnDelivery", StringComparison.OrdinalIgnoreCase);

            // COD orders get a head start so the Advanced COD plugin's own OrderPlaced
            // consumer has time to write its CodOrderInfo row before we read it (event
            // delivery order between two plugins is not guaranteed by nopCommerce).
            var codLookupDelay = isCod ? Math.Max(0, _settings.CodInfoLookupDelaySeconds) : 0;

            await SendCustomerMessagesAsync(order, isCod, codLookupDelay);
            await SendOwnerMessageAsync(order, isCod, codLookupDelay);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("WA-AKG: OrderPlacedConsumer failed", ex);
            await _errorLogService.LogAsync("OrderPlacedConsumer", ex.Message, ex.ToString());
        }
    }

    /// <summary>One combined order-confirmation message (+ COD due summary) to the customer.</summary>
    protected virtual async Task SendCustomerMessagesAsync(Nop.Core.Domain.Orders.Order order, bool isCod, int codLookupDelay)
    {
        if (!await _queueService.IsOptedInAsync(order.CustomerId))
            return;

        var phone = await _tokenService.GetOrderPhoneAsync(order);
        if (string.IsNullOrWhiteSpace(phone))
            return;

        if (_settings.EnableOrderPlaced)
        {
            var message = await _tokenService.BuildForOrderAsync(_settings.TemplateOrderPlaced, order);
            var (imageUrl, imageMimeType) = await _tokenService.GetOrderMainImageUrlAsync(order);

            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                // one message: the image with the full order text as its caption
                await _queueService.EnqueueAsync(phone, message, WaAkgDefaults.Events.OrderPlaced,
                    imageUrl, imageMimeType, order.Id, order.CustomerId);
            }
            else
            {
                // no picture found - logged so this shows up in the Errors tab instead of
                // silently disappearing, since a missing image is easy to mistake for a bug
                await _errorLogService.LogAsync("OrderPlaced.Image",
                    $"No product image resolved for order {order.Id} - the first item may have no picture, or the store URL/media settings could not produce a public link. Sent as text-only.",
                    null, phone);

                await _queueService.EnqueueAsync(phone, message, WaAkgDefaults.Events.OrderPlaced,
                    orderId: order.Id, customerId: order.CustomerId);
            }
        }

        // COD orders: ask for the small advance that confirms the order. If the customer pays the
        // advance from the order-details page before this goes out, CodAdvancePaidConsumer marks
        // this row as failed/superseded so it never actually sends.
        if (_settings.EnableCodAdvance && isCod && order.PaymentStatus == Nop.Core.Domain.Payments.PaymentStatus.Pending)
        {
            var codMessage = await _tokenService.BuildForOrderAsync(_settings.TemplateCodAdvance, order);
            await _queueService.EnqueueAsync(phone, codMessage, WaAkgDefaults.Events.CodAdvance,
                orderId: order.Id, customerId: order.CustomerId);
        }

        // COD due summary: how much is already paid vs. due on delivery - queued with the lookup
        // delay above so the Advanced COD plugin's row exists by the time this actually sends.
        if (isCod)
        {
            var codDueMessage = await _tokenService.BuildForOrderAsync(_settings.TemplateCodDue, order);
            await _queueService.EnqueueAsync(phone, codDueMessage, WaAkgDefaults.Events.CodDue,
                orderId: order.Id, customerId: order.CustomerId, minDelaySeconds: codLookupDelay);
        }
    }

    /// <summary>Owner/admin copy of the new order notification, with a link to the admin order page.</summary>
    protected virtual async Task SendOwnerMessageAsync(Nop.Core.Domain.Orders.Order order, bool isCod, int codLookupDelay)
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

        var template = isCod ? _settings.TemplateOwnerCod : _settings.TemplateOwnerOrderPlaced;
        var message = await _tokenService.BuildForOrderAsync(template, order);

        foreach (var number in numbers)
            await _queueService.EnqueueAsync(number, message, WaAkgDefaults.Events.OwnerNotification,
                orderId: order.Id, customerId: order.CustomerId,
                minDelaySeconds: isCod ? codLookupDelay : 0);
    }
}
