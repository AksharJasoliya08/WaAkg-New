using Nop.Core;

namespace Nop.Plugin.Misc.WaAkg.Domain;

/// <summary>
/// A single outbound WhatsApp message waiting to be delivered by the background task.
/// </summary>
public class QueuedWhatsAppMessage : BaseEntity
{
    /// <summary>Raw destination phone number (normalized to a JID at send time).</summary>
    public string Phone { get; set; }

    /// <summary>Message body (already token-replaced).</summary>
    public string Message { get; set; }

    /// <summary>Optional absolute URL or local path of an attachment.</summary>
    public string MediaUrl { get; set; }

    /// <summary>MIME type of the attachment, e.g. image/jpeg.</summary>
    public string MimeType { get; set; }

    /// <summary>Name of the automation event that produced this row.</summary>
    public string EventName { get; set; }

    /// <summary>Related order id, when applicable.</summary>
    public int? OrderId { get; set; }

    /// <summary>Related customer id, when applicable.</summary>
    public int? CustomerId { get; set; }

    /// <summary>Row creation date (UTC).</summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>Earliest moment this row may be sent (UTC).</summary>
    public DateTime ScheduledOnUtc { get; set; }

    /// <summary>Set once the gateway accepted the message (UTC).</summary>
    public DateTime? SentOnUtc { get; set; }

    /// <summary>Number of failed attempts.</summary>
    public int Retries { get; set; }

    /// <summary>Last error returned by the gateway.</summary>
    public string LastError { get; set; }
}
