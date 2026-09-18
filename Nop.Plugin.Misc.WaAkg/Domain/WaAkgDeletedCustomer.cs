using Nop.Core;

namespace Nop.Plugin.Misc.WaAkg.Domain;

/// <summary>
/// Tracks soft-deleted customers that were created via WhatsApp OTP.
/// This allows admins to either permanently delete or recover these accounts.
/// </summary>
public class WaAkgDeletedCustomer : BaseEntity
{
    /// <summary>
    /// The original Customer Id from the nopCommerce Customer table.
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// The WhatsApp phone number associated with this customer.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// The hidden email address generated for OTP account.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Username at time of deletion.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Full name at time of deletion.
    /// </summary>
    public string FullName { get; set; }

    /// <summary>
    /// UTC date/time when the customer was soft-deleted.
    /// </summary>
    public DateTime DeletedOnUtc { get; set; }

    /// <summary>
    /// Reason for deletion (if provided).
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Additional notes about this deletion.
    /// </summary>
    public string Notes { get; set; }
}
