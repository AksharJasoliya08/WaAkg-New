using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Plugin.Misc.WaAkg.Services;
using Nop.Services.Common;
using Nop.Services.Events;
using Nop.Services.Logging;

namespace Nop.Plugin.Misc.WaAkg.Consumers;

/// <summary>
/// Fires the "advance paid" WhatsApp message the moment the Advanced COD plugin's own
/// order-details "Pay Advance" flow actually completes a payment - never at order placement.
///
/// The Advanced COD plugin has no dedicated event for this; both its Partial and Full Payment
/// conversion paths end by calling nopCommerce's own IOrderService.UpdateOrderAsync, which fires
/// EntityUpdatedEvent&lt;Order&gt; (the same event OrderStatusChangedConsumer already listens on
/// for Delivered/Cancelled). This consumer detects the payment specifically by comparing the
/// CodOrderInfo.AdvancePaid amount against what was last seen for this order (kept in a generic
/// attribute) - any increase means new money was actually captured, regardless of which of the
/// two conversion paths (Partial/Full) produced it.
///
/// If a CodAdvance nudge is still sitting unsent in the queue when this fires, it is marked as
/// permanently failed with an explanatory note instead of being sent - the customer already paid,
/// so asking them to pay again would be wrong.
/// </summary>
public class CodAdvancePaidConsumer : IConsumer<EntityUpdatedEvent<Order>>
{
    protected const string LastSeenAdvancePaidAttribute = "WaAkgLastSeenAdvancePaid";

    protected readonly ICodBridgeService _codBridgeService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly ILogger _logger;
    protected readonly IWaAkgErrorLogService _errorLogService;
    protected readonly IWaAkgQueueService _queueService;
    protected readonly IWaAkgTokenService _tokenService;
    protected readonly WaAkgSettings _settings;

    public CodAdvancePaidConsumer(ICodBridgeService codBridgeService,
        IGenericAttributeService genericAttributeService,
        ILogger logger,
        IWaAkgErrorLogService errorLogService,
        IWaAkgQueueService queueService,
        IWaAkgTokenService tokenService,
        WaAkgSettings settings)
    {
        _codBridgeService = codBridgeService;
        _genericAttributeService = genericAttributeService;
        _logger = logger;
        _errorLogService = errorLogService;
        _queueService = queueService;
        _tokenService = tokenService;
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

            var codInfo = await _codBridgeService.GetOrderCodInfoAsync(order.Id);
            if (codInfo == null || codInfo.AdvancePaid <= 0)
                return;

            var lastSeen = await _genericAttributeService.GetAttributeAsync<decimal?>(order, LastSeenAdvancePaidAttribute) ?? 0m;

            // no new money captured since we last looked at this order - nothing to do
            if (codInfo.AdvancePaid <= lastSeen)
                return;

            await _genericAttributeService.SaveAttributeAsync(order, LastSeenAdvancePaidAttribute, codInfo.AdvancePaid);

            // supersede a still-pending "please pay the advance" nudge - the customer already paid
            var pendingNudges = await _queueService.SearchAsync(WaAkgDefaults.Events.CodAdvance, 1, 0, int.MaxValue);
            foreach (var pending in pendingNudges.Where(m => m.OrderId == order.Id))
            {
                pending.Retries = int.MaxValue;
                pending.LastError = "Superseded: advance paid from order details before this nudge went out.";
                await _queueService.UpdateAsync(pending);
            }

            var phone = await _tokenService.GetOrderPhoneAsync(order);
            if (string.IsNullOrWhiteSpace(phone))
                return;

            if (!await _queueService.IsOptedInAsync(order.CustomerId))
                return;

            var message = await _tokenService.BuildForOrderAsync(_settings.TemplateCodAdvancePaid, order);
            await _queueService.EnqueueAsync(phone, message, WaAkgDefaults.Events.CodAdvancePaid,
                orderId: order.Id, customerId: order.CustomerId);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("WA-AKG: CodAdvancePaidConsumer failed", ex);
            await _errorLogService.LogAsync("CodAdvancePaidConsumer", ex.Message, ex.ToString());
        }
    }
}
