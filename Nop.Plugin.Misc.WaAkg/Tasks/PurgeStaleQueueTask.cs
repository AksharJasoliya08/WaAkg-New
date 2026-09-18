using Nop.Plugin.Misc.WaAkg.Services;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Misc.WaAkg.Tasks;

/// <summary>
/// Removes queue rows that have been sitting unsent for more than a month (dead numbers,
/// abandoned campaigns, permanently failed sends) and old error log rows, so both tables
/// don't grow forever. Runs once a day.
/// </summary>
public class PurgeStaleQueueTask : IScheduleTask
{
    #region Constants

    /// <summary>How old an unsent row must be before it is dropped.</summary>
    protected const int STALE_AFTER_DAYS = 30;

    #endregion

    #region Fields

    protected readonly ILogger _logger;
    protected readonly IWaAkgQueueService _queueService;
    protected readonly IWaAkgErrorLogService _errorLogService;

    #endregion

    #region Ctor

    public PurgeStaleQueueTask(ILogger logger,
        IWaAkgQueueService queueService,
        IWaAkgErrorLogService errorLogService)
    {
        _logger = logger;
        _queueService = queueService;
        _errorLogService = errorLogService;
    }

    #endregion

    #region Methods

    /// <summary>Execute the task.</summary>
    public async Task ExecuteAsync()
    {
        try
        {
            var removedMessages = await _queueService.PurgeStalePendingAsync(STALE_AFTER_DAYS);
            var removedErrors = await _errorLogService.PurgeAsync(STALE_AFTER_DAYS);

            if (removedMessages > 0 || removedErrors > 0)
                await _logger.InformationAsync(
                    $"WA-AKG: purged {removedMessages} queue row(s) and {removedErrors} error log row(s) older than {STALE_AFTER_DAYS} days.");
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("WA-AKG: PurgeStaleQueueTask failed", ex);
        }
    }

    #endregion
}
