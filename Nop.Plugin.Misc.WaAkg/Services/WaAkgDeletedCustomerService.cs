using LinqToDB;
using Nop.Core;
using Nop.Data;
using Nop.Plugin.Misc.WaAkg.Domain;
using Nop.Services.Customers;

namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Service implementation for managing soft-deleted OTP customers.
/// </summary>
public class WaAkgDeletedCustomerService : IWaAkgDeletedCustomerService
{
    #region Fields

    protected readonly IRepository<WaAkgDeletedCustomer> _repository;
    protected readonly ICustomerService _customerService;
    protected readonly IRepository<Customer> _customerRepository;

    #endregion

    #region Ctor

    public WaAkgDeletedCustomerService(
        IRepository<WaAkgDeletedCustomer> repository,
        ICustomerService customerService,
        IRepository<Customer> customerRepository)
    {
        _repository = repository;
        _customerService = customerService;
        _customerRepository = customerRepository;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Record a customer as soft-deleted.
    /// </summary>
    public virtual async Task<WaAkgDeletedCustomer> RecordDeletionAsync(int customerId, string reason = null, string notes = null)
    {
        var customer = await _customerService.GetCustomerByIdAsync(customerId);
        if (customer == null)
            return null;

        // Check if already recorded
        var existing = await GetByCustomerIdAsync(customerId);
        if (existing != null)
            return existing;

        var deletedRecord = new WaAkgDeletedCustomer
        {
            CustomerId = customerId,
            Phone = customer.Phone ?? string.Empty,
            Email = customer.Email ?? string.Empty,
            Username = customer.Username ?? string.Empty,
            FullName = $"{customer.FirstName} {customer.LastName}".Trim(),
            DeletedOnUtc = DateTime.UtcNow,
            Reason = reason ?? string.Empty,
            Notes = notes ?? string.Empty
        };

        await _repository.InsertAsync(deletedRecord, false);
        return deletedRecord;
    }

    /// <summary>
    /// Get all soft-deleted customers (paged).
    /// </summary>
    public virtual async Task<IPagedList<WaAkgDeletedCustomer>> SearchAsync(string searchPhone = null, string searchEmail = null,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var query = _repository.Table;

        if (!string.IsNullOrWhiteSpace(searchPhone))
            query = query.Where(x => x.Phone.Contains(searchPhone));

        if (!string.IsNullOrWhiteSpace(searchEmail))
            query = query.Where(x => x.Email.Contains(searchEmail));

        query = query.OrderByDescending(x => x.DeletedOnUtc);

        var totalCount = await LinqToDB.AsyncExtensions.CountAsync(query);
        var data = await LinqToDB.AsyncExtensions.ToListAsync(
            query.Skip(pageIndex * pageSize).Take(pageSize)
        );

        return new PagedList<WaAkgDeletedCustomer>(data, pageIndex, pageSize, totalCount);
    }

    /// <summary>
    /// Get a soft-deleted customer record by ID.
    /// </summary>
    public virtual async Task<WaAkgDeletedCustomer> GetByIdAsync(int id)
        => await _repository.GetByIdAsync(id, cache => default);

    /// <summary>
    /// Get a soft-deleted customer record by original CustomerId.
    /// </summary>
    public virtual async Task<WaAkgDeletedCustomer> GetByCustomerIdAsync(int customerId)
    {
        return await LinqToDB.AsyncExtensions.FirstOrDefaultAsync(
            _repository.Table.Where(x => x.CustomerId == customerId)
        );
    }

    /// <summary>
    /// Permanently delete the record (and optionally the customer from nopCommerce if still exists).
    /// </summary>
    public virtual async Task PermanentDeleteAsync(int id, bool deleteFromNopCommerce = false)
    {
        var record = await GetByIdAsync(id);
        if (record == null)
            return;

        if (deleteFromNopCommerce && record.CustomerId > 0)
        {
            var customer = await _customerService.GetCustomerByIdAsync(record.CustomerId);
            if (customer != null)
            {
                // Hard delete from nopCommerce
                await _customerRepository.DeleteAsync(customer, false);
            }
        }

        // Delete the soft-delete record itself
        await _repository.DeleteAsync(record, false);
    }

    /// <summary>
    /// Recover a soft-deleted customer back to nopCommerce Customer table.
    /// </summary>
    public virtual async Task<bool> RecoverAsync(int id)
    {
        var record = await GetByIdAsync(id);
        if (record == null)
            return false;

        // Check if customer still exists in nopCommerce (wasn't hard deleted)
        var customer = await _customerService.GetCustomerByIdAsync(record.CustomerId);
        
        if (customer != null)
        {
            // Customer still exists but was marked as deleted - just undelete it
            customer.Deleted = false;
            await _customerService.UpdateCustomerAsync(customer);
            
            // Remove the soft-delete record
            await _repository.DeleteAsync(record, false);
            return true;
        }

        // Customer no longer exists in nopCommerce - cannot recover
        // In this case, we keep the record but return false
        return false;
    }

    #endregion
}
