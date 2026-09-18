namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Normalized result of a WA-AKG gateway call.
/// </summary>
public class WaAkgResult
{
    /// <summary>True only when HTTP is 2xx AND the payload "status" flag is true.</summary>
    public bool Success { get; set; }

    /// <summary>HTTP status code (0 when the request never completed).</summary>
    public int HttpCode { get; set; }

    /// <summary>Human readable error, null on success.</summary>
    public string Error { get; set; }

    /// <summary>Raw response body, kept for the admin diagnostics box.</summary>
    public string Raw { get; set; }

    /// <summary>Build a failed result.</summary>
    public static WaAkgResult Fail(string error, int httpCode = 0, string raw = null)
        => new() { Success = false, Error = error, HttpCode = httpCode, Raw = raw };

    /// <summary>Build a successful result.</summary>
    public static WaAkgResult Ok(int httpCode, string raw)
        => new() { Success = true, HttpCode = httpCode, Raw = raw };
}
