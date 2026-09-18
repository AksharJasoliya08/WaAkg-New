using Nop.Core;

namespace Nop.Plugin.Misc.WaAkg.Domain;

/// <summary>
/// Every gateway/plugin error, kept separately from the queue table so failures are visible
/// even for actions that never created a queue row (e.g. Test Connection, a broadcast upload).
/// </summary>
public class WaAkgErrorLog : BaseEntity
{
    /// <summary>Short machine-readable source, e.g. "SendText", "TestConnection", "Broadcast".</summary>
    public string Source { get; set; }

    /// <summary>Phone number involved, if any.</summary>
    public string Phone { get; set; }

    /// <summary>The error message shown to the admin.</summary>
    public string Message { get; set; }

    /// <summary>Raw gateway/exception detail, kept for diagnostics.</summary>
    public string Detail { get; set; }

    public DateTime CreatedOnUtc { get; set; }
}
