using Nop.Core;
using Nop.Plugin.Misc.WaAkg.Domain;

namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Records and lists plugin/gateway errors for the admin "Errors" tab.
/// </summary>
public interface IWaAkgErrorLogService
{
    /// <summary>Record one error. Never throws - logging failures must not break the caller.</summary>
    Task LogAsync(string source, string message, string detail = null, string phone = null);

    /// <summary>Most recent errors first.</summary>
    Task<IPagedList<WaAkgErrorLog>> GetRecentAsync(int pageIndex = 0, int pageSize = 50);

    /// <summary>Delete rows older than the given number of days.</summary>
    Task<int> PurgeAsync(int olderThanDays);

    /// <summary>Remove every row.</summary>
    Task ClearAsync();
}
