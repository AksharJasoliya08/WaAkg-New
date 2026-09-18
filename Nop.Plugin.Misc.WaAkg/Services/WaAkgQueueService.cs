using LinqToDB;
using Nop.Core;
using Nop.Data;
using Nop.Plugin.Misc.WaAkg.Domain;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Helpers;

namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Default queue implementation backed by the QueuedWhatsAppMessage table.
/// </summary>
public class WaAkgQueueService : IWaAkgQueueService
{
    #region Fields

    protected readonly IRepository<QueuedWhatsAppMessage> _repository;
    protected readonly ICustomerService _customerService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly WaAkgSettings _settings;

    #endregion

    #region Ctor

    public WaAkgQueueService(IRepository<QueuedWhatsAppMessage> repository,
        ICustomerService customerService,
        IGenericAttributeService genericAttributeService,
        IDateTimeHelper dateTimeHelper,
        WaAkgSettings settings)
    {
        _repository = repository;
        _customerService = customerService;
        _genericAttributeService = genericAttributeService;
        _dateTimeHelper = dateTimeHelper;
        _settings = settings;
    }

    #endregion

    #region Methods

    /// <summary>Insert a message, automatically picking the next anti-ban safe slot.</summary>
    public virtual async Task<QueuedWhatsAppMessage> EnqueueAsync(string phone, string message, string eventName,
        string mediaUrl = null, string mimeType = null, int? orderId = null, int? customerId = null,
        int minDelaySeconds = 0)
    {
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(message))
            return null;

        var now = DateTime.UtcNow;
        var gap = TimeSpan.FromSeconds(Math.Max(1, _settings.SendDelaySeconds));

        // last scheduled slot of any pending row, so nothing is ever sent back to back
        var lastSlot = await LinqToDB.AsyncExtensions.FirstOrDefaultAsync(
            _repository.Table
                .Where(m => m.SentOnUtc == null)
                .OrderByDescending(m => m.ScheduledOnUtc)
                .Select(m => (DateTime?)m.ScheduledOnUtc)
        );

        var scheduled = lastSlot.HasValue && lastSlot.Value.Add(gap) > now
            ? lastSlot.Value.Add(gap)
            : now;

        if (minDelaySeconds > 0)
        {
            var earliest = now.AddSeconds(minDelaySeconds);
            if (scheduled < earliest)
                scheduled = earliest;
        }

        scheduled = await ApplyQuietHoursAsync(scheduled);

        var entity = new QueuedWhatsAppMessage
        {
            Phone = phone.Trim(),
            Message = message,
            MediaUrl = mediaUrl,
            MimeType = mimeType,
            EventName = eventName,
            OrderId = orderId,
            CustomerId = customerId,
            CreatedOnUtc = now,
            ScheduledOnUtc = scheduled,
            Retries = 0
        };

        await _repository.InsertAsync(entity, false);

        return entity;
    }

    /// <summary>Rows that are unsent, due and still within the retry budget.</summary>
    public virtual async Task<IList<QueuedWhatsAppMessage>> GetDueMessagesAsync(int count)
    {
        var now = DateTime.UtcNow;
        var maxRetries = Math.Max(1, _settings.MaxRetries);

        return await _repository.Table
            .Where(m => m.SentOnUtc == null && m.ScheduledOnUtc <= now && m.Retries < maxRetries)
            .OrderBy(m => m.ScheduledOnUtc)
            .ThenBy(m => m.Id)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>Paged admin search. status: 0 = all, 1 = pending, 2 = sent, 3 = failed.</summary>
    public virtual async Task<IPagedList<QueuedWhatsAppMessage>> SearchAsync(string eventName = null, int status = 0,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var maxRetries = Math.Max(1, _settings.MaxRetries);

        var query = _repository.Table;

        if (!string.IsNullOrWhiteSpace(eventName))
            query = query.Where(m => m.EventName == eventName);

        query = status switch
        {
            1 => query.Where(m => m.SentOnUtc == null && m.Retries < maxRetries),
            2 => query.Where(m => m.SentOnUtc != null),
            3 => query.Where(m => m.SentOnUtc == null && m.Retries >= maxRetries),
            _ => query
        };

        query = query.OrderByDescending(m => m.Id);

        var totalCount = await LinqToDB.AsyncExtensions.CountAsync(query);
        var data = await LinqToDB.AsyncExtensions.ToListAsync(
            query.Skip(pageIndex * pageSize).Take(pageSize)
        );

        return new PagedList<QueuedWhatsAppMessage>(data, pageIndex, pageSize, totalCount);
    }

    public virtual async Task<QueuedWhatsAppMessage> GetByIdAsync(int id)
        => await _repository.GetByIdAsync(id, cache => default);

    public virtual async Task InsertAsync(QueuedWhatsAppMessage message)
        => await _repository.InsertAsync(message, false);

    public virtual async Task UpdateAsync(QueuedWhatsAppMessage message)
        => await _repository.UpdateAsync(message, false);

    public virtual async Task DeleteAsync(QueuedWhatsAppMessage message)
        => await _repository.DeleteAsync(message, false);

    /// <summary>Number of messages actually delivered in the last rolling 24h.</summary>
    public virtual async Task<int> GetSentCountLast24HoursAsync()
    {
        var from = DateTime.UtcNow.AddHours(-24);
        return await _repository.Table.CountAsync(m => m.SentOnUtc != null && m.SentOnUtc > from);
    }

    /// <summary>Shift a UTC moment out of the configured quiet window (store time).</summary>
    public virtual async Task<DateTime> ApplyQuietHoursAsync(DateTime utcDate)
    {
        var start = _settings.QuietHourStart;
        var end = _settings.QuietHourEnd;

        // quiet hours disabled
        if (start == end)
            return utcDate;

        var timeZone = await _dateTimeHelper.GetCurrentTimeZoneAsync();
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcDate, timeZone);

        bool inQuietWindow = start > end
            ? local.Hour >= start || local.Hour < end          // window crosses midnight (22 -> 08)
            : local.Hour >= start && local.Hour < end;

        if (!inQuietWindow)
            return utcDate;

        var resume = local.Date.AddHours(end);
        if (local.Hour >= start && start > end)
            resume = local.Date.AddDays(1).AddHours(end);

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(resume, DateTimeKind.Unspecified), timeZone);
    }

    /// <summary>True when the customer may be messaged.</summary>
    public virtual async Task<bool> IsOptedInAsync(int customerId)
    {
        if (!_settings.RequireOptIn)
            return true;

        if (customerId <= 0)
            return false;

        var customer = await _customerService.GetCustomerByIdAsync(customerId);
        if (customer == null)
            return false;

        var value = await _genericAttributeService.GetAttributeAsync<string>(customer, WaAkgDefaults.OptInAttribute);

        // never explicitly answered -> fall back to the configured default state of the checkbox
        if (string.IsNullOrEmpty(value))
            return _settings.OptInCheckedByDefault;

        return value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Delete rows older than the given number of days.</summary>
    public virtual async Task<int> PurgeAsync(int olderThanDays)
    {
        var threshold = DateTime.UtcNow.AddDays(-Math.Abs(olderThanDays));
        var old = await _repository.Table.Where(m => m.CreatedOnUtc < threshold).ToListAsync();
        if (old.Any())
            await _repository.DeleteAsync(old, false);
        return old.Count;
    }

    /// <summary>Delete unsent (pending or permanently failed) rows older than the given number of
    /// days. Sent rows are left alone - this only clears out dead/abandoned queue entries.</summary>
    public virtual async Task<int> PurgeStalePendingAsync(int olderThanDays)
    {
        var threshold = DateTime.UtcNow.AddDays(-Math.Abs(olderThanDays));
        var stale = await _repository.Table
            .Where(m => m.SentOnUtc == null && m.CreatedOnUtc < threshold)
            .ToListAsync();

        if (stale.Any())
            await _repository.DeleteAsync(stale, false);

        return stale.Count;
    }

    #endregion
}
