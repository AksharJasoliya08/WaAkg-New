using Nop.Core;
using Nop.Plugin.Misc.WaAkg.Domain;

namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Persistence and scheduling of outbound WhatsApp messages.
/// Consumers never call the gateway directly - they enqueue here.
/// </summary>
public interface IWaAkgQueueService
{
    /// <summary>Insert a message, automatically picking the next anti-ban safe slot.
    /// When <paramref name="minDelaySeconds"/> is set, the row is never scheduled earlier than that
    /// many seconds from now (used to let another plugin's own OrderPlaced consumer finish first).</summary>
    Task<QueuedWhatsAppMessage> EnqueueAsync(string phone, string message, string eventName,
        string mediaUrl = null, string mimeType = null, int? orderId = null, int? customerId = null,
        int minDelaySeconds = 0);

    /// <summary>Rows that are unsent, due and still within the retry budget.</summary>
    Task<IList<QueuedWhatsAppMessage>> GetDueMessagesAsync(int count);

    /// <summary>Paged admin search.</summary>
    Task<IPagedList<QueuedWhatsAppMessage>> SearchAsync(string eventName = null, int status = 0,
        int pageIndex = 0, int pageSize = int.MaxValue);

    Task<QueuedWhatsAppMessage> GetByIdAsync(int id);
    Task InsertAsync(QueuedWhatsAppMessage message);
    Task UpdateAsync(QueuedWhatsAppMessage message);
    Task DeleteAsync(QueuedWhatsAppMessage message);

    /// <summary>Number of messages actually delivered in the last rolling 24h.</summary>
    Task<int> GetSentCountLast24HoursAsync();

    /// <summary>Shift a UTC moment out of the configured quiet window.</summary>
    Task<DateTime> ApplyQuietHoursAsync(DateTime utcDate);

    /// <summary>True when the customer may be messaged (opt-in rules).</summary>
    Task<bool> IsOptedInAsync(int customerId);

    /// <summary>Delete rows older than the given number of days.</summary>
    Task<int> PurgeAsync(int olderThanDays);

    /// <summary>Delete unsent (pending or permanently failed) rows older than the given number of
    /// days, leaving sent rows untouched.</summary>
    Task<int> PurgeStalePendingAsync(int olderThanDays);
}
