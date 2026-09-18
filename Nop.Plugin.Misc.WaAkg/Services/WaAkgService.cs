using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nop.Services.Logging;

namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// HttpClient based implementation of <see cref="IWaAkgService"/>.
/// Server-to-server only: the API key never leaves the backend.
///
/// Verified against the gateway's own OpenAPI doc (GET {base}/api/docs, v1.2.0):
/// the universal endpoint POST /api/messages/{sessionId}/{jid}/send expects
///   { "message": { "text": "..." } }                                    for text
///   { "message": { "image": { "url": "..." }, "caption": "..." } }      for an image
///   { "message": { "document": { "url": "..." }, "caption": "..." } }   for a document
/// i.e. "message" is always a nested object, never a bare string, and every
/// attachment is sent as a URL - there is no multipart/form-data upload route.
/// </summary>
public class WaAkgService : IWaAkgService
{
    #region Fields

    protected readonly IHttpClientFactory _httpClientFactory;
    protected readonly ILogger _logger;
    protected readonly WaAkgSettings _settings;

    #endregion

    #region Ctor

    public WaAkgService(IHttpClientFactory httpClientFactory,
        ILogger logger,
        WaAkgSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _settings = settings;
    }

    #endregion

    #region Utilities

    /// <summary>Create a preconfigured client (base address + auth header).</summary>
    protected virtual HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(WaAkgDefaults.HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(25);
        if (!client.DefaultRequestHeaders.Contains("X-API-Key"))
            client.DefaultRequestHeaders.Add("X-API-Key", _settings.ApiKey?.Trim());
        return client;
    }

    /// <summary>Absolute endpoint URL built from the configured base URL.</summary>
    protected virtual string Url(string relative)
        => $"{(_settings.ApiBaseUrl ?? string.Empty).TrimEnd('/')}/{relative.TrimStart('/')}";

    /// <summary>Interpret the gateway envelope { status, message, error, data }.</summary>
    protected virtual WaAkgResult Interpret(HttpResponseMessage response, string body)
    {
        var code = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
            return WaAkgResult.Fail(DescribeHttpError(code, body), code, body);

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("status", out var status) &&
                status.ValueKind == JsonValueKind.False)
            {
                var message = doc.RootElement.TryGetProperty("message", out var m) ? m.ToString() : "Gateway returned status=false";
                return WaAkgResult.Fail($"{message} - {Truncate(body, 300)}", code, body);
            }
        }
        catch (JsonException)
        {
            // non JSON 2xx body - treat as success, keep raw for diagnostics
        }

        return WaAkgResult.Ok(code, body);
    }

    /// <summary>Map known gateway failures to actionable admin text, but always keep the raw body visible.</summary>
    protected virtual string DescribeHttpError(int code, string body)
    {
        var hint = code switch
        {
            401 or 403 => "API key invalid or regenerated. Update it on the Connection tab.",
            404 => "Session not found or disconnected - wrong SessionId, or the session needs a QR re-scan.",
            500 => "Gateway failed to send the message (see raw response below).",
            503 => "WhatsApp session is not ready. Open the gateway dashboard and re-scan the QR code.",
            400 => "Invalid request - jid and message are required.",
            _ => "Unexpected response."
        };

        return $"HTTP {code} - {hint} Gateway: {Truncate(body, 300)}";
    }

    protected static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];

    /// <summary>Execute a request with retry + linear backoff on transient failures.</summary>
    protected virtual async Task<WaAkgResult> ExecuteAsync(Func<HttpRequestMessage> requestFactory, string operation)
    {
        var attempts = Math.Max(1, _settings.MaxRetries);
        WaAkgResult last = null;

        for (var i = 1; i <= attempts; i++)
        {
            try
            {
                using var client = CreateClient();
                using var request = requestFactory();
                using var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                last = Interpret(response, body);

                if (last.Success)
                    return last;

                // authentication / routing problems will not be fixed by retrying
                if (last.HttpCode is 401 or 403 or 404)
                    return last;
            }
            catch (TaskCanceledException)
            {
                last = WaAkgResult.Fail("Timeout - the gateway did not answer in 25s. If the direct port is blocked by the host, keep the base URL pointed at the port 80 proxy.");
            }
            catch (Exception ex)
            {
                last = WaAkgResult.Fail($"{ex.GetType().Name}: {ex.Message}");
            }

            if (i < attempts)
                await Task.Delay(5000);
        }

        await _logger.ErrorAsync($"WA-AKG: {operation} failed. {last?.Error}");
        return last ?? WaAkgResult.Fail("Unknown error");
    }

    /// <summary>
    /// Build the "message" object for the universal /send endpoint from a MIME type.
    /// Every attachment is referenced by URL - the gateway has no binary upload route.
    /// </summary>
    protected virtual object BuildMessagePayload(string text, string mediaUrl, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl))
            return new { text };

        var normalizedMime = (mimeType ?? string.Empty).ToLowerInvariant();

        if (normalizedMime.StartsWith("image/"))
            return new { image = new { url = mediaUrl }, caption = text };

        if (normalizedMime.StartsWith("video/"))
            return new { video = new { url = mediaUrl }, caption = text };

        if (normalizedMime.StartsWith("audio/"))
            return new { audio = new { url = mediaUrl } };

        // documents (PDF, etc.) and anything unrecognized
        return new { document = new { url = mediaUrl }, caption = text, fileName = FileNameFromUrl(mediaUrl) };
    }

    protected virtual string FileNameFromUrl(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var name = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }
        }
        catch
        {
            // fall through
        }

        return "attachment";
    }

    #endregion

    #region Methods

    /// <summary>Normalize a phone number to {digits}@s.whatsapp.net.</summary>
    public virtual string ToJid(string phone)
    {
        var digits = Regex.Replace(phone ?? string.Empty, @"\D", string.Empty);

        if (digits.Length == 10)
            digits = $"{_settings.DefaultCountryCode}{digits}";

        // handle numbers stored as 0XXXXXXXXXX with a leading trunk zero
        if (digits.Length == 11 && digits.StartsWith("0"))
            digits = $"{_settings.DefaultCountryCode}{digits[1..]}";

        return $"{digits}@s.whatsapp.net";
    }

    /// <summary>GET /api/sessions.</summary>
    public virtual Task<WaAkgResult> TestConnectionAsync()
        => ExecuteAsync(() => new HttpRequestMessage(HttpMethod.Get, Url("/api/sessions")), "TestConnection");

    /// <summary>
    /// Send a plain text message via the universal endpoint.
    /// Body: { "message": { "text": "..." } } - "message" is an object, never a bare string.
    /// </summary>
    public virtual async Task<WaAkgResult> SendTextAsync(string phone, string message)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return WaAkgResult.Fail("Empty phone number");

        var jid = ToJid(phone);
        var payload = JsonSerializer.Serialize(new { message = BuildMessagePayload(message, null, null) });

        return await ExecuteAsync(() => new HttpRequestMessage(HttpMethod.Post,
            Url($"/api/messages/{_settings.SessionId}/{jid}/send"))
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }, $"SendText to {jid}");
    }

    /// <summary>
    /// Send an image/video/audio/document. The gateway takes a URL, not an upload -
    /// mediaUrl must be reachable by the gateway server (a local file path will not work).
    /// </summary>
    public virtual async Task<WaAkgResult> SendMediaAsync(string phone, string fileUrlOrPath, string mimeType, string caption)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return WaAkgResult.Fail("Empty phone number");
        if (string.IsNullOrWhiteSpace(fileUrlOrPath))
            return WaAkgResult.Fail("Empty media url");

        if (!Uri.TryCreate(fileUrlOrPath, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return WaAkgResult.Fail(
                "The gateway only accepts a public http(s) URL for media - a local file path cannot be sent. " +
                "Upload the file somewhere reachable by the gateway server first.");
        }

        var jid = ToJid(phone);
        var payload = JsonSerializer.Serialize(new { message = BuildMessagePayload(caption, fileUrlOrPath, mimeType) });

        return await ExecuteAsync(() => new HttpRequestMessage(HttpMethod.Post,
            Url($"/api/messages/{_settings.SessionId}/{jid}/send"))
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }, $"SendMedia to {jid}");
    }

    /// <summary>Server-side bulk broadcast with a delay between recipients.</summary>
    public virtual Task<WaAkgResult> SendBroadcastAsync(IEnumerable<string> phones, string message, int delayMs)
    {
        var recipients = (phones ?? Enumerable.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(ToJid)
            .Distinct()
            .ToList();

        if (!recipients.Any())
            return Task.FromResult(WaAkgResult.Fail("No valid recipients"));

        var payload = JsonSerializer.Serialize(new
        {
            recipients,
            message = BuildMessagePayload(message, null, null),
            delay = Math.Max(delayMs, _settings.SendDelaySeconds * 1000)
        });

        return ExecuteAsync(() => new HttpRequestMessage(HttpMethod.Post,
            Url($"/api/messages/{_settings.SessionId}/broadcast"))
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }, $"Broadcast to {recipients.Count} recipients");
    }

    #endregion
}
