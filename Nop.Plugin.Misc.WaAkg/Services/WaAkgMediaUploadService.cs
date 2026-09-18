using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Infrastructure;

namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Saves uploaded broadcast media under wwwroot/uploaded/wa-akg/ so it is reachable by a
/// plain http(s) URL - exactly what the WA-AKG gateway's media message format requires.
/// </summary>
public class WaAkgMediaUploadService : IWaAkgMediaUploadService
{
    #region Fields

    protected const string RelativeFolder = "uploaded/wa-akg";
    protected static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4", ".mov", ".pdf", ".doc", ".docx"
    };

    protected readonly INopFileProvider _fileProvider;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public WaAkgMediaUploadService(INopFileProvider fileProvider, IWebHelper webHelper)
    {
        _fileProvider = fileProvider;
        _webHelper = webHelper;
    }

    #endregion

    #region Utilities

    /// <summary>Best-effort MIME type from the file extension when the browser didn't send one.</summary>
    protected virtual string ResolveMimeType(string extension, string browserProvidedType)
    {
        if (!string.IsNullOrWhiteSpace(browserProvidedType) && browserProvidedType != "application/octet-stream")
            return browserProvidedType;

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
    }

    #endregion

    #region Methods

    /// <summary>Save the file and return its public absolute URL, or null if nothing was uploaded.</summary>
    public virtual async Task<(string url, string mimeType)> SaveAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return (null, null);

        var extension = _fileProvider.GetFileExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new InvalidOperationException(
                $"File type '{extension}' is not allowed. Use an image, video, PDF or Word document.");

        var folder = _fileProvider.GetAbsolutePath(RelativeFolder);
        _fileProvider.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = _fileProvider.Combine(folder, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
            await file.CopyToAsync(stream);

        var storeLocation = _webHelper.GetStoreLocation().TrimEnd('/');
        var url = $"{storeLocation}/{RelativeFolder}/{fileName}";

        return (url, ResolveMimeType(extension, file.ContentType));
    }

    #endregion
}
