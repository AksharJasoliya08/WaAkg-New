using Nop.Plugin.Misc.WaAkg.Services;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Misc.WaAkg.Tasks;

/// <summary>
/// Drains the WhatsApp queue, one message at a time, respecting the
/// configured gap between sends and the rolling daily cap.
/// </summary>
public class ProcessQueueTask : IScheduleTask
{
    #region Constants

    /// <summary>Maximum rows picked up in a single run.</summary>
    protected const int BATCH_SIZE = 25;

    #endregion

    #region Fields

    protected readonly ILogger _logger;
    protected readonly IWaAkgQueueService _queueService;
    protected readonly IWaAkgService _waAkgService;
    protected readonly IWaAkgErrorLogService _errorLogService;
    protected readonly WaAkgSettings _settings;

    #endregion

    #region Ctor

    public ProcessQueueTask(ILogger logger,
        IWaAkgQueueService queueService,
        IWaAkgService waAkgService,
        IWaAkgErrorLogService errorLogService,
        WaAkgSettings settings)
    {
        _logger = logger;
        _queueService = queueService;
        _waAkgService = waAkgService;
        _errorLogService = errorLogService;
        _settings = settings;
    }

    #endregion

    #region Methods

    /// <summary>Execute the task.</summary>
    public async Task ExecuteAsync()
    {
        if (!_settings.Enabled)
            return;

        var sentToday = await _queueService.GetSentCountLast24HoursAsync();
        var budget = _settings.DailySendLimit - sentToday;

        if (budget <= 0)
        {
            await _logger.WarningAsync($"WA-AKG: daily send limit ({_settings.DailySendLimit}) reached, queue paused.");
            return;
        }

        var messages = await _queueService.GetDueMessagesAsync(Math.Min(BATCH_SIZE, budget));
        if (!messages.Any())
            return;

        var gap = TimeSpan.FromSeconds(Math.Max(1, _settings.SendDelaySeconds));
        var first = true;

        foreach (var message in messages)
        {
            if (!first)
                await Task.Delay(gap);
            first = false;

            try
            {
                var result = string.IsNullOrWhiteSpace(message.MediaUrl)
                    ? await _waAkgService.SendTextAsync(message.Phone, message.Message)
                    : await _waAkgService.SendMediaAsync(message.Phone, message.MediaUrl, message.MimeType, message.Message);

                if (result.Success)
                {
                    message.SentOnUtc = DateTime.UtcNow;
                    message.LastError = null;
                }
                else
                {
                    message.Retries++;
                    message.LastError = result.Error;
                    // linear backoff between attempts
                    message.ScheduledOnUtc = await _queueService.ApplyQuietHoursAsync(
                        DateTime.UtcNow.AddMinutes(5 * message.Retries));

                    await _errorLogService.LogAsync(
                        string.IsNullOrWhiteSpace(message.MediaUrl) ? "SendText" : "SendMedia",
                        result.Error, result.Raw, message.Phone);
                }
            }
            catch (Exception ex)
            {
                message.Retries++;
                message.LastError = $"{ex.GetType().Name}: {ex.Message}";
                await _logger.ErrorAsync($"WA-AKG: queue row {message.Id} failed", ex);
                await _errorLogService.LogAsync("ProcessQueueTask", ex.Message, ex.ToString(), message.Phone);
            }

            await _queueService.UpdateAsync(message);
        }
    }

    #endregion
}
