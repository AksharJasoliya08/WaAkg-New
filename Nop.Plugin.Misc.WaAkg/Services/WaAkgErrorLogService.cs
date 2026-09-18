using Nop.Core;
using Nop.Data;
using Nop.Plugin.Misc.WaAkg.Domain;

namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Default implementation of <see cref="IWaAkgErrorLogService"/>.
/// </summary>
public class WaAkgErrorLogService : IWaAkgErrorLogService
{
    protected readonly IRepository<WaAkgErrorLog> _repository;

    public WaAkgErrorLogService(IRepository<WaAkgErrorLog> repository)
    {
        _repository = repository;
    }

    /// <summary>Record one error. Never throws - logging failures must not break the caller.</summary>
    public virtual async Task LogAsync(string source, string message, string detail = null, string phone = null)
    {
        try
        {
            await _repository.InsertAsync(new WaAkgErrorLog
            {
                Source = source,
                Phone = phone,
                Message = message?.Length > 1000 ? message[..1000] : message,
                Detail = detail?.Length > 2000 ? detail[..2000] : detail,
                CreatedOnUtc = DateTime.UtcNow
            }, false);
        }
        catch
        {
            // logging must never throw back into the caller
        }
    }

    /// <summary>Most recent errors first.</summary>
    public virtual async Task<IPagedList<WaAkgErrorLog>> GetRecentAsync(int pageIndex = 0, int pageSize = 50)
    {
        var query = _repository.Table.OrderByDescending(e => e.Id);
        var totalCount = await query.CountAsync();
        var data = await query.Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();
        return new PagedList<WaAkgErrorLog>(data, pageIndex, pageSize, totalCount);
    }

    /// <summary>Delete rows older than the given number of days.</summary>
    public virtual async Task<int> PurgeAsync(int olderThanDays)
    {
        var threshold = DateTime.UtcNow.AddDays(-Math.Abs(olderThanDays));
        var old = await _repository.Table.Where(e => e.CreatedOnUtc < threshold).ToListAsync();
        if (old.Any())
            await _repository.DeleteAsync(old, false);
        return old.Count;
    }

    /// <summary>Remove every row.</summary>
    public virtual async Task ClearAsync()
    {
        var all = await _repository.Table.ToListAsync();
        if (all.Any())
            await _repository.DeleteAsync(all, false);
    }
}
