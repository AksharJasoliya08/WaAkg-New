using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.WaAkg.Models;

/// <summary>
/// Model of the Admin &gt; Configuration page.
/// </summary>
public record ConfigurationModel : BaseNopModel
{
    public ConfigurationModel()
    {
        AvailableTokens = new List<string>();
        Queue = new QueuedMessageSearchModel();
        Broadcast = new BroadcastModel();
    }

    public int ActiveStoreScopeConfiguration { get; set; }

    #region Connection

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.ApiBaseUrl")]
    public string ApiBaseUrl { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.ApiKey")]
    [DataType(DataType.Password)]
    public string ApiKey { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.SessionId")]
    public string SessionId { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.Enabled")]
    public bool Enabled { get; set; }

    #endregion

    #region Automation

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.SendDelaySeconds")]
    public int SendDelaySeconds { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.MaxRetries")]
    public int MaxRetries { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.DailySendLimit")]
    public int DailySendLimit { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.QuietHourStart")]
    public int QuietHourStart { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.QuietHourEnd")]
    public int QuietHourEnd { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.RequireOptIn")]
    public bool RequireOptIn { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.OptInCheckedByDefault")]
    public bool OptInCheckedByDefault { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.CartReminderHours")]
    public int CartReminderHours { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.CartReminderCoupon")]
    public string CartReminderCoupon { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.PaymentLink")]
    public string PaymentLink { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.DefaultCountryCode")]
    public string DefaultCountryCode { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.CodInfoLookupDelaySeconds")]
    public int CodInfoLookupDelaySeconds { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.OrderTrackUrlPattern")]
    public string OrderTrackUrlPattern { get; set; }

    public bool EnableOrderPlaced { get; set; }
    public bool EnableCodAdvance { get; set; }
    public bool EnableOrderPaid { get; set; }
    public bool EnableShipment { get; set; }
    public bool EnableDelivered { get; set; }
    public bool EnableCancelled { get; set; }
    public bool EnableWelcome { get; set; }
    public bool EnableCartReminder { get; set; }

    #endregion

    #region Templates

    public string TemplateOrderPlaced { get; set; }
    public string TemplateCodAdvance { get; set; }
    public string TemplateCodAdvancePaid { get; set; }
    public string TemplateOrderPaid { get; set; }
    public string TemplateShipment { get; set; }
    public string TemplateDelivered { get; set; }
    public string TemplateCancelled { get; set; }
    public string TemplateWelcome { get; set; }
    public string TemplateCartReminder { get; set; }
    public string TemplateCodDue { get; set; }

    public IList<string> AvailableTokens { get; set; }

    #endregion

    #region Owner notifications

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.OwnerNotificationsEnabled")]
    public bool OwnerNotificationsEnabled { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.OwnerNotificationNumbers")]
    public string OwnerNotificationNumbers { get; set; }

    public string TemplateOwnerOrderPlaced { get; set; }
    public string TemplateOwnerCod { get; set; }

    #endregion

    #region WhatsApp OTP Login (Module B v2)

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.EnableWhatsAppOtp")]
    public bool EnableWhatsAppOtp { get; set; }

    public string OtpMessageTemplate { get; set; }
    public string OtpPasswordTemplate { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.OtpExpiryMinutes")]
    public int OtpExpiryMinutes { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.OtpMaxAttempts")]
    public int OtpMaxAttempts { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.OtpResendCooldownSec")]
    public int OtpResendCooldownSec { get; set; }

    [NopResourceDisplayName("Plugins.Misc.WaAkg.Fields.BypassNativeLoginForGuestCheckout")]
    public bool BypassNativeLoginForGuestCheckout { get; set; }

    #endregion

    #region Nested models

    public QueuedMessageSearchModel Queue { get; set; }

    public BroadcastModel Broadcast { get; set; }

    #endregion
}
