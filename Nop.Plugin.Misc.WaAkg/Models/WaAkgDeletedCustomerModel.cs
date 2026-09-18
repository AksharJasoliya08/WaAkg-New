using Nop.Web.Framework.Models;

namespace Nop.Plugin.Misc.WaAkg.Models;

/// <summary>
/// Model for soft-deleted customer records.
/// </summary>
public class WaAkgDeletedCustomerModel : BaseNopEntityModel
{
    public int CustomerId { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Username { get; set; }
    public string FullName { get; set; }
    public DateTime DeletedOnUtc { get; set; }
    public string Reason { get; set; }
    public string Notes { get; set; }
}

/// <summary>
/// Search model for deleted customers grid.
/// </summary>
public class WaAkgDeletedCustomerSearchModel : BaseSearchModel
{
    public string SearchPhone { get; set; }
    public string SearchEmail { get; set; }
}
