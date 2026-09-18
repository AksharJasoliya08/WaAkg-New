using Nop.Web.Framework.Models;

namespace Nop.Plugin.Misc.WaAkg.Models;

/// <summary>
/// One row of the admin "Errors" grid.
/// </summary>
public record WaAkgErrorLogModel : BaseNopEntityModel
{
    public string Source { get; set; }
    public string Phone { get; set; }
    public string Message { get; set; }
    public string Detail { get; set; }
    public DateTime CreatedOn { get; set; }
}
