using Nop.Core;
using Nop.Plugin.Misc.WaAkg.Domain;

namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Service for managing soft-deleted OTP customers.
/// </summary>
public interface IWaAkgDeletedCustomerService
{
    /// <summary>
    /// Record a customer as soft-deleted.
    /// </summary>
    Task<WaAkgDeletedCustomer> RecordDeletionAsync(int customerId, string reason = null, string notes = null);

    /// <summary>
    /// Get all soft-deleted customers (paged).
    /// </summary>
    Task<IPagedList<WaAkgDeletedCustomer>> SearchAsync(string searchPhone = null, string searchEmail = null,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Get a soft-deleted customer record by ID.
    /// </summary>
    Task<WaAkgDeletedCustomer> GetByIdAsync(int id);

    /// <summary>
    /// Get a soft-deleted customer record by original CustomerId.
    /// </summary>
    Task<WaAkgDeletedCustomer> GetByCustomerIdAsync(int customerId);

    /// <summary>
    /// Permanently delete the record (and optionally the customer from nopCommerce if still exists).
    /// </summary>
    Task PermanentDeleteAsync(int id, bool deleteFromNopCommerce = false);

    /// <summary>
    /// Recover a soft-deleted customer back to nopCommerce Customer table.
    /// </summary>
    Task<bool> RecoverAsync(int id);
}
