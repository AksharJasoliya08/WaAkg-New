using Nop.Core;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Plugin.Misc.WaAkg.Components;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Misc.WaAkg;

/// <summary>
/// WhatsApp Automation Suite (WA-AKG).
/// Misc plugin + widget (the widget only renders the checkout opt-in checkbox).
/// </summary>
public class WaAkgPlugin : BasePlugin, IMiscPlugin, IWidgetPlugin
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly IScheduleTaskService _scheduleTaskService;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public WaAkgPlugin(ILocalizationService localizationService,
        IScheduleTaskService scheduleTaskService,
        ISettingService settingService,
        IWebHelper webHelper)
    {
        _localizationService = localizationService;
        _scheduleTaskService = scheduleTaskService;
        _settingService = settingService;
        _webHelper = webHelper;
    }

    #endregion

    #region Properties

    /// <summary>The widget is only an opt-in checkbox, keep it out of the widget list.</summary>
    public bool HideInWidgetList => true;

    #endregion

    #region Utilities

    /// <summary>Create the scheduled task when it does not exist yet.</summary>
    protected virtual async Task EnsureTaskAsync(string name, string type, int seconds)
    {
        if (await _scheduleTaskService.GetTaskByTypeAsync(type) != null)
            return;

        await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
        {
            Name = name,
            Type = type,
            Seconds = seconds,
            Enabled = true,
            StopOnError = false
        });
    }

    /// <summary>Remove the scheduled task if present.</summary>
    protected virtual async Task RemoveTaskAsync(string type)
    {
        var task = await _scheduleTaskService.GetTaskByTypeAsync(type);
        if (task != null)
            await _scheduleTaskService.DeleteTaskAsync(task);
    }

    #endregion

    #region Methods

    /// <summary>Admin configuration page URL.</summary>
    public override string GetConfigurationPageUrl()
        => $"{_webHelper.GetStoreLocation()}Admin/WaAkgAdmin/Configure";

    /// <summary>Widget zones where the checkout opt-in checkbox and the WhatsApp OTP modal are rendered.</summary>
    public Task<IList<string>> GetWidgetZonesAsync()
    {
        // string literals on purpose: zones are matched by name at render time
        return Task.FromResult<IList<string>>(new List<string>
        {
            "checkout_billing_address_bottom",
            "op_checkout_billing_address_bottom",
            "checkout_confirm_top",
            "body_start_html_tag_after",
            PublicWidgetZones.Footer
        });
    }

    /// <summary>View component that renders the checkbox, the OTP modal, or the login-bypass
    /// script, depending on the zone.</summary>
    public Type GetWidgetViewComponent(string widgetZone)
    {
        if (string.Equals(widgetZone, "body_start_html_tag_after", StringComparison.OrdinalIgnoreCase))
            return typeof(WaOtpWidgetViewComponent);

        if (string.Equals(widgetZone, PublicWidgetZones.Footer, StringComparison.OrdinalIgnoreCase))
            return typeof(WaAkgLoginBypassScriptViewComponent);

        return typeof(WaAkgOptInViewComponent);
    }

    /// <summary>Install the plugin.</summary>
    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new WaAkgSettings());

        await EnsureTaskAsync(WaAkgDefaults.ProcessQueueTaskName, WaAkgDefaults.ProcessQueueTaskType, 60);
        await EnsureTaskAsync(WaAkgDefaults.CartReminderTaskName, WaAkgDefaults.CartReminderTaskType, 6 * 60 * 60);
        await EnsureTaskAsync(WaAkgDefaults.PurgeStaleQueueTaskName, WaAkgDefaults.PurgeStaleQueueTaskType, 24 * 60 * 60);

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Misc.WaAkg.Tab.Connection"] = "Connection",
            ["Plugins.Misc.WaAkg.Tab.Templates"] = "Templates",
            ["Plugins.Misc.WaAkg.Tab.Automation"] = "Automation",
            ["Plugins.Misc.WaAkg.Tab.Queue"] = "Queue",
            ["Plugins.Misc.WaAkg.Tab.Broadcast"] = "Broadcast",
            ["Plugins.Misc.WaAkg.Tab.WhatsAppOtp"] = "WhatsApp OTP Login",

            ["Plugins.Misc.WaAkg.Fields.ApiBaseUrl"] = "API base URL",
            ["Plugins.Misc.WaAkg.Fields.ApiBaseUrl.Hint"] = "Base URL of the WA-AKG gateway. Keep the port 80 proxy path when the host blocks the direct port.",
            ["Plugins.Misc.WaAkg.Fields.ApiKey"] = "API key",
            ["Plugins.Misc.WaAkg.Fields.ApiKey.Hint"] = "Sent as the X-API-Key header on every request. Never rendered on the storefront.",
            ["Plugins.Misc.WaAkg.Fields.SessionId"] = "Session ID",
            ["Plugins.Misc.WaAkg.Fields.SessionId.Hint"] = "The session ID, not the session name. A wrong value returns HTTP 404.",
            ["Plugins.Misc.WaAkg.Fields.Enabled"] = "Enabled",
            ["Plugins.Misc.WaAkg.Fields.Enabled.Hint"] = "Master switch for every automation.",

            ["Plugins.Misc.WaAkg.Fields.SendDelaySeconds"] = "Delay between sends (seconds)",
            ["Plugins.Misc.WaAkg.Fields.SendDelaySeconds.Hint"] = "Minimum gap between two outbound messages.",
            ["Plugins.Misc.WaAkg.Fields.MaxRetries"] = "Max retries",
            ["Plugins.Misc.WaAkg.Fields.MaxRetries.Hint"] = "Attempts before a queued message is marked as failed.",
            ["Plugins.Misc.WaAkg.Fields.DailySendLimit"] = "Daily send limit",
            ["Plugins.Misc.WaAkg.Fields.DailySendLimit.Hint"] = "Hard cap per rolling 24 hours.",
            ["Plugins.Misc.WaAkg.Fields.QuietHourStart"] = "Quiet hours start",
            ["Plugins.Misc.WaAkg.Fields.QuietHourStart.Hint"] = "Store time. Messages created inside the window are deferred.",
            ["Plugins.Misc.WaAkg.Fields.QuietHourEnd"] = "Quiet hours end",
            ["Plugins.Misc.WaAkg.Fields.QuietHourEnd.Hint"] = "Store time. Set equal to the start value to disable quiet hours.",
            ["Plugins.Misc.WaAkg.Fields.RequireOptIn"] = "Require opt-in",
            ["Plugins.Misc.WaAkg.Fields.RequireOptIn.Hint"] = "Only message customers who accepted WhatsApp updates.",
            ["Plugins.Misc.WaAkg.Fields.OptInCheckedByDefault"] = "Opt-in checked by default",
            ["Plugins.Misc.WaAkg.Fields.OptInCheckedByDefault.Hint"] = "Initial state of the checkout checkbox.",
            ["Plugins.Misc.WaAkg.Fields.CartReminderHours"] = "Cart idle hours",
            ["Plugins.Misc.WaAkg.Fields.CartReminderHours.Hint"] = "A cart must be untouched this long before a reminder is queued.",
            ["Plugins.Misc.WaAkg.Fields.CartReminderCoupon"] = "Cart reminder coupon",
            ["Plugins.Misc.WaAkg.Fields.PaymentLink"] = "Payment link",
            ["Plugins.Misc.WaAkg.Fields.PaymentLink.Hint"] = "Value of the %PaymentLink% token.",
            ["Plugins.Misc.WaAkg.Fields.DefaultCountryCode"] = "Default country code",
            ["Plugins.Misc.WaAkg.Fields.DefaultCountryCode.Hint"] = "Prefixed to 10 digit numbers before building the JID.",
            ["Plugins.Misc.WaAkg.Fields.CodInfoLookupDelaySeconds"] = "COD info lookup delay (seconds)",
            ["Plugins.Misc.WaAkg.Fields.OrderTrackUrlPattern"] = "Order details URL pattern (%TrackUrl%)",
            ["Plugins.Misc.WaAkg.Fields.OwnerNotificationsEnabled"] = "Send owner/admin notifications",
            ["Plugins.Misc.WaAkg.Fields.OwnerNotificationNumbers"] = "Owner WhatsApp number(s)",

            ["Plugins.Misc.WaAkg.Queue.Phone"] = "Phone",
            ["Plugins.Misc.WaAkg.Queue.Event"] = "Event",
            ["Plugins.Misc.WaAkg.Queue.Status"] = "Status",
            ["Plugins.Misc.WaAkg.Queue.Retries"] = "Retries",
            ["Plugins.Misc.WaAkg.Queue.LastError"] = "Last error",
            ["Plugins.Misc.WaAkg.Queue.Message"] = "Message",
            ["Plugins.Misc.WaAkg.Queue.CreatedOn"] = "Created on",
            ["Plugins.Misc.WaAkg.Queue.ScheduledOn"] = "Scheduled on",

            ["Plugins.Misc.WaAkg.Broadcast.Recipients"] = "Recipients",
            ["Plugins.Misc.WaAkg.Broadcast.Recipients.Hint"] = "Comma or newline separated phone numbers.",
            ["Plugins.Misc.WaAkg.Broadcast.CsvFile"] = "CSV file",
            ["Plugins.Misc.WaAkg.Broadcast.Message"] = "Message",
            ["Plugins.Misc.WaAkg.Broadcast.MediaUrl"] = "Media URL",
            ["Plugins.Misc.WaAkg.Broadcast.MediaFile"] = "Upload media",
            ["Plugins.Misc.WaAkg.Broadcast.MimeType"] = "Media MIME type",
            ["Plugins.Misc.WaAkg.Broadcast.DelayMs"] = "Delay between recipients (ms)",
            ["Plugins.Misc.WaAkg.Broadcast.UseQueue"] = "Send through the internal queue",
            ["Plugins.Misc.WaAkg.Broadcast.UseQueue.Hint"] = "Recommended. Applies throttling, retries, quiet hours and the daily cap.",

            ["Plugins.Misc.WaAkg.OptIn.Label"] = "Get order updates on WhatsApp",
            ["Plugins.Misc.WaAkg.TestConnection"] = "Test connection",
            ["Plugins.Misc.WaAkg.SendTest"] = "Send test message",

            ["Plugins.Misc.WaAkg.Fields.EnableWhatsAppOtp"] = "Enable WhatsApp OTP checkout login",
            ["Plugins.Misc.WaAkg.Fields.EnableWhatsAppOtp.Hint"] = "Shows a non-dismissible popup at checkout that requires guests to verify their phone via a WhatsApp OTP before continuing.",
            ["Plugins.Misc.WaAkg.Fields.OtpExpiryMinutes"] = "OTP expiry (minutes)",
            ["Plugins.Misc.WaAkg.Fields.OtpMaxAttempts"] = "Max wrong attempts",
            ["Plugins.Misc.WaAkg.Fields.OtpResendCooldownSec"] = "Resend cooldown (seconds)",
            ["Plugins.Misc.WaAkg.Fields.BypassNativeLoginForGuestCheckout"] = "Bypass native login page for guest checkout",
            ["Plugins.Misc.WaAkg.Fields.BypassNativeLoginForGuestCheckout.Hint"] = "When a guest would normally be sent to nopCommerce's native Login/Register page (Checkout button on the cart page or mini-cart, with \"Skip Login/Guest Page\" off in DirectCheckout), send them straight to /checkout instead so the WhatsApp OTP popup handles login/registration. Has no effect unless \"Enable WhatsApp OTP checkout login\" above is also on."
        });

        await base.InstallAsync();
    }

    /// <summary>Uninstall the plugin.</summary>
    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<WaAkgSettings>();

        await RemoveTaskAsync(WaAkgDefaults.ProcessQueueTaskType);
        await RemoveTaskAsync(WaAkgDefaults.CartReminderTaskType);
        await RemoveTaskAsync(WaAkgDefaults.PurgeStaleQueueTaskType);

        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Misc.WaAkg");

        await base.UninstallAsync();
    }

    #endregion
}
