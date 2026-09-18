using Microsoft.AspNetCore.Http;

namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Saves an admin-uploaded file (broadcast image/video/pdf) somewhere under the store's own
/// public wwwroot and returns the absolute URL the gateway can fetch it from. The gateway
/// only accepts media by URL - there is no binary upload route - so every attachment must be
/// reachable over http(s) before it is queued.
/// </summary>
public interface IWaAkgMediaUploadService
{
    /// <summary>Save the file and return its public absolute URL, or null if nothing was uploaded.</summary>
    Task<(string url, string mimeType)> SaveAsync(IFormFile file);
}
