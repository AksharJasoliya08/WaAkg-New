using Microsoft.AspNetCore.Http;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.WaAkg.Models;

/// <summary>
/// Model of the bulk broadcast tab.
/// </summary>
public record BroadcastModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Misc.WaAkg.Broadcast.Recipients")]
    public string Recipients { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Broadcast.CsvFile")]
    public IFormFile CsvFile { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Broadcast.Message")]
    public string Message { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Broadcast.MediaUrl")]
    public string MediaUrl { get; set; }

    /// <summary>Optional file upload (image/video/pdf). When present, it is saved to the store's
    /// public wwwroot and its resulting URL is used instead of <see cref="MediaUrl"/>.</summary>
    [NopResourceDisplayName("Plugins.Misc.WaAkg.Broadcast.MediaFile")]
    public IFormFile MediaFile { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Broadcast.MimeType")]
    public string MimeType { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Broadcast.DelayMs")]
    public int DelayMs { get; set; } = 10000;

    /// <summary>Queue the messages instead of hitting the broadcast endpoint directly.</summary>
    [NopResourceDisplayName("Plugins.Misc.WaAkg.Broadcast.UseQueue")]
    public bool UseQueue { get; set; } = true;

    public string Result { get; set; }
}
