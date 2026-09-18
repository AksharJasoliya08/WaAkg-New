namespace Nop.Plugin.Misc.WaAkg;

/// <summary>
/// Constants used across the WA-AKG plugin.
/// </summary>
public static class WaAkgDefaults
{
    /// <summary>Plugin system name.</summary>
    public const string SystemName = "Misc.WaAkg";

    /// <summary>Named HttpClient used for every gateway call.</summary>
    public const string HttpClientName = "WaAkg";

    /// <summary>Generic attribute key holding the customer WhatsApp opt-in flag.</summary>
    public const string OptInAttribute = "WaAkgOptIn";

    /// <summary>Generic attribute key holding the UTC date of the last cart reminder.</summary>
    public const string LastCartReminderAttribute = "WaAkgLastCartReminderUtc";

    /// <summary>Generic attribute key holding the WhatsApp phone number used to auto-register a customer (Module B v2).</summary>
    public const string OtpPhoneAttribute = "WaAkgPhone";

    /// <summary>Generic attribute key: "true" while the customer must change their auto-generated temp password.</summary>
    public const string MustChangePasswordAttribute = "WaAkgMustChangePassword";

    /// <summary>In-memory cache key prefix: the current OTP code for a phone. {0} = normalized phone.</summary>
    public const string OtpCodeCacheKey = "WaAkg.Otp.{0}";

    /// <summary>In-memory cache key prefix: resend cooldown lock for a phone. {0} = normalized phone.</summary>
    public const string OtpResendCacheKey = "WaAkg.Otp.RL.{0}";

    /// <summary>In-memory cache key prefix: wrong-attempt counter for a phone. {0} = normalized phone.</summary>
    public const string OtpAttemptsCacheKey = "WaAkg.Otp.Attempts.{0}";

    /// <summary>Domain used to build a unique, never-colliding placeholder email for WhatsApp-only accounts.</summary>
    public const string OtpAccountEmailDomain = "wa.themalas.com";

    /// <summary>Queue processing scheduled task.</summary>
    public const string ProcessQueueTaskName = "WhatsApp (WA-AKG) - process send queue";
    public const string ProcessQueueTaskType = "Nop.Plugin.Misc.WaAkg.Tasks.ProcessQueueTask, Nop.Plugin.Misc.WaAkg";

    /// <summary>Abandoned cart reminder scheduled task.</summary>
    public const string CartReminderTaskName = "WhatsApp (WA-AKG) - abandoned cart reminder";
    public const string CartReminderTaskType = "Nop.Plugin.Misc.WaAkg.Tasks.CartReminderTask, Nop.Plugin.Misc.WaAkg";

    /// <summary>Stale/pending queue and error log cleanup scheduled task.</summary>
    public const string PurgeStaleQueueTaskName = "WhatsApp (WA-AKG) - purge stale queue rows";
    public const string PurgeStaleQueueTaskType = "Nop.Plugin.Misc.WaAkg.Tasks.PurgeStaleQueueTask, Nop.Plugin.Misc.WaAkg";

    /// <summary>Event names stored on queue rows.</summary>
    public static class Events
    {
        public const string OrderPlaced = "OrderPlaced";
        public const string CodAdvance = "CodAdvance";
        public const string OrderPaid = "OrderPaid";
        public const string Shipment = "Shipment";
        public const string Delivered = "Delivered";
        public const string Cancelled = "Cancelled";
        public const string Welcome = "Welcome";
        public const string CartReminder = "CartReminder";
        public const string Broadcast = "Broadcast";
        public const string Manual = "Manual";
        public const string CodDue = "CodDue";
        public const string OwnerNotification = "OwnerNotification";
        public const string CodAdvancePaid = "CodAdvancePaid";
    }
}
