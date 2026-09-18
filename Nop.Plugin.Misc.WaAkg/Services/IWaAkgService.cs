namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Low level client for the WA-AKG WhatsApp gateway.
/// </summary>
public interface IWaAkgService
{
    /// <summary>GET /api/sessions — used by the admin "Test connection" button.</summary>
    Task<WaAkgResult> TestConnectionAsync();

    /// <summary>POST /api/messages/{sessionId}/{jid}/send</summary>
    Task<WaAkgResult> SendTextAsync(string phone, string message);

    /// <summary>POST /api/messages/{sessionId}/{jid}/media (multipart/form-data).</summary>
    Task<WaAkgResult> SendMediaAsync(string phone, string fileUrlOrPath, string mimeType, string caption);

    /// <summary>POST /api/messages/{sessionId}/broadcast</summary>
    Task<WaAkgResult> SendBroadcastAsync(IEnumerable<string> phones, string message, int delayMs);

    /// <summary>Convert a raw phone number into a WhatsApp JID.</summary>
    string ToJid(string phone);
}
