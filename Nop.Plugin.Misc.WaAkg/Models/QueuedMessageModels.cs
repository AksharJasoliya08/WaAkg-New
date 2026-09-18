using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.WaAkg.Models;

/// <summary>
/// Single queue row shown in the admin grid.
/// </summary>
public record QueuedMessageModel : BaseNopEntityModel
{
    [NopResourceDisplayName("Plugins.Misc.WaAkg.Queue.Phone")]
    public string Phone { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Queue.Event")]
    public string EventName { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Queue.Status")]
    public string Status { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Queue.Retries")]
    public int Retries { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Queue.LastError")]
    public string LastError { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Queue.Message")]
    public string Message { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Queue.CreatedOn")]
    public DateTime CreatedOn { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Queue.ScheduledOn")]
    public DateTime ScheduledOn { get; set; }

    public DateTime? SentOn { get; set; }
}

/// <summary>
/// Filters of the queue grid.
/// </summary>
public record QueuedMessageSearchModel : BaseSearchModel
{
    public QueuedMessageSearchModel()
    {
        AvailableStatuses = new List<SelectListItem>();
        AvailableEvents = new List<SelectListItem>();
    }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Queue.Status")]
    public int SearchStatusId { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Queue.Event")]
    public string SearchEventName { get; set; }

    public IList<SelectListItem> AvailableStatuses { get; set; }
    public IList<SelectListItem> AvailableEvents { get; set; }
}

/// <summary>
/// Paged result of the queue grid.
/// </summary>
public record QueuedMessageListModel : BasePagedListModel<QueuedMessageModel>
{
}
