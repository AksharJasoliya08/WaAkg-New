using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.Misc.WaAkg.Domain;
using Nop.Plugin.Misc.WaAkg.Models;
using Nop.Plugin.Misc.WaAkg.Services;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Models.Extensions;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.WaAkg.Controllers;

/// <summary>
/// Admin area controller of the WhatsApp Automation Suite.
/// </summary>
[AuthorizeAdmin]
[Area("Admin")]
[AutoValidateAntiforgeryToken]
public class WaAkgAdminController : BasePluginController
{
    #region Fields

    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IPermissionService _permissionService;
    protected readonly ISettingService _settingService;
    protected readonly IWaAkgQueueService _queueService;
    protected readonly IWaAkgService _waAkgService;
    protected readonly IWaAkgTokenService _tokenService;
    protected readonly IWaAkgErrorLogService _errorLogService;
    protected readonly IWaAkgMediaUploadService _mediaUploadService;
    protected readonly IWaAkgDeletedCustomerService _deletedCustomerService;
    protected readonly WaAkgSettings _settings;

    #endregion

    #region Ctor

    public WaAkgAdminController(IDateTimeHelper dateTimeHelper,
        ILocalizationService localizationService,
        INotificationService notificationService,
        IPermissionService permissionService,
        ISettingService settingService,
        IWaAkgQueueService queueService,
        IWaAkgService waAkgService,
        IWaAkgTokenService tokenService,
        IWaAkgErrorLogService errorLogService,
        IWaAkgMediaUploadService mediaUploadService,
        IWaAkgDeletedCustomerService deletedCustomerService,
        WaAkgSettings settings)
    {
        _dateTimeHelper = dateTimeHelper;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _permissionService = permissionService;
        _settingService = settingService;
        _queueService = queueService;
        _waAkgService = waAkgService;
        _tokenService = tokenService;
        _errorLogService = errorLogService;
        _mediaUploadService = mediaUploadService;
        _deletedCustomerService = deletedCustomerService;
        _settings = settings;
    }

    #endregion

    #region Utilities

    /// <summary>True when the current user may manage plugins.</summary>
    protected virtual async Task<bool> HasAccessAsync()
        => await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS);

    /// <summary>Fill the lookup lists of the queue grid.</summary>
    protected virtual QueuedMessageSearchModel PrepareSearchModel(QueuedMessageSearchModel model)
    {
        model ??= new QueuedMessageSearchModel();

        model.AvailableStatuses = new List<SelectListItem>
        {
            new() { Value = "0", Text = "All" },
            new() { Value = "1", Text = "Pending" },
            new() { Value = "2", Text = "Sent" },
            new() { Value = "3", Text = "Failed" }
        };

        model.AvailableEvents = new List<SelectListItem> { new() { Value = "", Text = "All" } };
        foreach (var name in new[]
                 {
                     WaAkgDefaults.Events.OrderPlaced, WaAkgDefaults.Events.CodAdvance,
                     WaAkgDefaults.Events.OrderPaid, WaAkgDefaults.Events.Shipment,
                     WaAkgDefaults.Events.Delivered, WaAkgDefaults.Events.Cancelled,
                     WaAkgDefaults.Events.Welcome, WaAkgDefaults.Events.CartReminder,
                     WaAkgDefaults.Events.Broadcast, WaAkgDefaults.Events.Manual
                 })
            model.AvailableEvents.Add(new SelectListItem { Value = name, Text = name });

        model.SetGridPageSize();

        return model;
    }

    /// <summary>Human readable status of a queue row.</summary>
    protected virtual string DescribeStatus(QueuedWhatsAppMessage message)
    {
        if (message.SentOnUtc.HasValue)
            return "Sent";
        return message.Retries >= Math.Max(1, _settings.MaxRetries) ? "Failed" : "Pending";
    }

    /// <summary>Split a free text / CSV recipient list into phone numbers, de-duplicated after
    /// normalizing formatting differences (spaces, dashes, +91 prefix, etc.) so the same number
    /// typed two different ways is only ever queued once.</summary>
    protected virtual IList<string> ParseRecipients(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        return raw
            .Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => value.Any(char.IsDigit))
            .GroupBy(value => NormalizePhoneKey(value))
            .Select(group => group.First())
            .ToList();
    }

    /// <summary>Digits-only key used purely for de-duplication, matching how the gateway JID is built.</summary>
    protected virtual string NormalizePhoneKey(string phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());

        if (digits.Length == 10)
            digits = _settings.DefaultCountryCode?.Trim() + digits;
        else if (digits.Length == 11 && digits.StartsWith("0"))
            digits = _settings.DefaultCountryCode?.Trim() + digits[1..];

        return digits;
    }

    #endregion

    #region Configure

    /// <summary>Render the configuration page.</summary>
    public virtual async Task<IActionResult> Configure()
    {
        if (!await HasAccessAsync())
            return AccessDeniedView();

        var model = new ConfigurationModel
        {
            ApiBaseUrl = _settings.ApiBaseUrl,
            ApiKey = _settings.ApiKey,
            SessionId = _settings.SessionId,
            Enabled = _settings.Enabled,

            SendDelaySeconds = _settings.SendDelaySeconds,
            MaxRetries = _settings.MaxRetries,
            DailySendLimit = _settings.DailySendLimit,
            QuietHourStart = _settings.QuietHourStart,
            QuietHourEnd = _settings.QuietHourEnd,
            RequireOptIn = _settings.RequireOptIn,
            OptInCheckedByDefault = _settings.OptInCheckedByDefault,
            CartReminderHours = _settings.CartReminderHours,
            CartReminderCoupon = _settings.CartReminderCoupon,
            PaymentLink = _settings.PaymentLink,
            DefaultCountryCode = _settings.DefaultCountryCode,

            EnableOrderPlaced = _settings.EnableOrderPlaced,
            EnableCodAdvance = _settings.EnableCodAdvance,
            EnableOrderPaid = _settings.EnableOrderPaid,
            EnableShipment = _settings.EnableShipment,
            EnableDelivered = _settings.EnableDelivered,
            EnableCancelled = _settings.EnableCancelled,
            EnableWelcome = _settings.EnableWelcome,
            EnableCartReminder = _settings.EnableCartReminder,

            TemplateOrderPlaced = _settings.TemplateOrderPlaced,
            TemplateCodAdvance = _settings.TemplateCodAdvance,
            TemplateCodAdvancePaid = _settings.TemplateCodAdvancePaid,
            TemplateOrderPaid = _settings.TemplateOrderPaid,
            TemplateShipment = _settings.TemplateShipment,
            TemplateDelivered = _settings.TemplateDelivered,
            TemplateCancelled = _settings.TemplateCancelled,
            TemplateWelcome = _settings.TemplateWelcome,
            TemplateCartReminder = _settings.TemplateCartReminder,
            TemplateCodDue = _settings.TemplateCodDue,

            OwnerNotificationsEnabled = _settings.OwnerNotificationsEnabled,
            OwnerNotificationNumbers = _settings.OwnerNotificationNumbers,
            TemplateOwnerOrderPlaced = _settings.TemplateOwnerOrderPlaced,
            TemplateOwnerCod = _settings.TemplateOwnerCod,
            CodInfoLookupDelaySeconds = _settings.CodInfoLookupDelaySeconds,
            OrderTrackUrlPattern = _settings.OrderTrackUrlPattern,

            EnableWhatsAppOtp = _settings.EnableWhatsAppOtp,
            OtpMessageTemplate = _settings.OtpMessageTemplate,
            OtpPasswordTemplate = _settings.OtpPasswordTemplate,
            OtpExpiryMinutes = _settings.OtpExpiryMinutes,
            OtpMaxAttempts = _settings.OtpMaxAttempts,
            OtpResendCooldownSec = _settings.OtpResendCooldownSec,
            BypassNativeLoginForGuestCheckout = _settings.BypassNativeLoginForGuestCheckout,

            AvailableTokens = _tokenService.GetAllowedTokens()
        };

        model.Queue = PrepareSearchModel(model.Queue);

        return View("~/Plugins/Misc.WaAkg/Views/Configure.cshtml", model);
    }

    /// <summary>Persist the configuration.</summary>
    [HttpPost, ActionName("Configure")]
    [FormValueRequired("save")]
    public virtual async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!await HasAccessAsync())
            return AccessDeniedView();

        if (!ModelState.IsValid)
            return await Configure();

        _settings.ApiBaseUrl = model.ApiBaseUrl?.Trim();
        // the password field renders empty on reload; only overwrite when the admin actually typed something
        if (!string.IsNullOrWhiteSpace(model.ApiKey))
            _settings.ApiKey = model.ApiKey.Trim();
        _settings.SessionId = model.SessionId?.Trim();
        _settings.Enabled = model.Enabled;

        _settings.SendDelaySeconds = Math.Max(1, model.SendDelaySeconds);
        _settings.MaxRetries = Math.Max(1, model.MaxRetries);
        _settings.DailySendLimit = Math.Max(1, model.DailySendLimit);
        _settings.QuietHourStart = Math.Clamp(model.QuietHourStart, 0, 23);
        _settings.QuietHourEnd = Math.Clamp(model.QuietHourEnd, 0, 23);
        _settings.RequireOptIn = model.RequireOptIn;
        _settings.OptInCheckedByDefault = model.OptInCheckedByDefault;
        _settings.CartReminderHours = Math.Max(1, model.CartReminderHours);
        _settings.CartReminderCoupon = model.CartReminderCoupon;
        _settings.PaymentLink = model.PaymentLink;
        _settings.DefaultCountryCode = string.IsNullOrWhiteSpace(model.DefaultCountryCode)
            ? "91" : model.DefaultCountryCode.Trim();
        _settings.CodInfoLookupDelaySeconds = Math.Max(0, model.CodInfoLookupDelaySeconds);
        _settings.OrderTrackUrlPattern = string.IsNullOrWhiteSpace(model.OrderTrackUrlPattern)
            ? "{0}orderdetails/{1}" : model.OrderTrackUrlPattern.Trim();

        _settings.EnableOrderPlaced = model.EnableOrderPlaced;
        _settings.EnableCodAdvance = model.EnableCodAdvance;
        _settings.EnableOrderPaid = model.EnableOrderPaid;
        _settings.EnableShipment = model.EnableShipment;
        _settings.EnableDelivered = model.EnableDelivered;
        _settings.EnableCancelled = model.EnableCancelled;
        _settings.EnableWelcome = model.EnableWelcome;
        _settings.EnableCartReminder = model.EnableCartReminder;

        _settings.TemplateOrderPlaced = model.TemplateOrderPlaced;
        _settings.TemplateCodAdvance = model.TemplateCodAdvance;
        _settings.TemplateCodAdvancePaid = model.TemplateCodAdvancePaid;
        _settings.TemplateOrderPaid = model.TemplateOrderPaid;
        _settings.TemplateShipment = model.TemplateShipment;
        _settings.TemplateDelivered = model.TemplateDelivered;
        _settings.TemplateCancelled = model.TemplateCancelled;
        _settings.TemplateWelcome = model.TemplateWelcome;
        _settings.TemplateCartReminder = model.TemplateCartReminder;
        _settings.TemplateCodDue = model.TemplateCodDue;

        _settings.OwnerNotificationsEnabled = model.OwnerNotificationsEnabled;
        _settings.OwnerNotificationNumbers = model.OwnerNotificationNumbers;
        _settings.TemplateOwnerOrderPlaced = model.TemplateOwnerOrderPlaced;
        _settings.TemplateOwnerCod = model.TemplateOwnerCod;

        _settings.EnableWhatsAppOtp = model.EnableWhatsAppOtp;
        _settings.OtpMessageTemplate = model.OtpMessageTemplate;
        _settings.OtpPasswordTemplate = model.OtpPasswordTemplate;
        _settings.OtpExpiryMinutes = Math.Max(1, model.OtpExpiryMinutes);
        _settings.OtpMaxAttempts = Math.Max(1, model.OtpMaxAttempts);
        _settings.OtpResendCooldownSec = Math.Max(1, model.OtpResendCooldownSec);
        _settings.BypassNativeLoginForGuestCheckout = model.BypassNativeLoginForGuestCheckout;

        await _settingService.SaveSettingAsync(_settings);
        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(
            await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return await Configure();
    }

    #endregion

    #region Connection

    /// <summary>Ping the gateway and report the outcome.</summary>
    [HttpPost]
    public virtual async Task<IActionResult> TestConnection(string apiBaseUrl, string apiKey, string sessionId)
    {
        if (!await HasAccessAsync())
            return Json(new { success = false, message = "Access denied" });

        // use the values currently typed in the form, without saving them
        if (!string.IsNullOrWhiteSpace(apiBaseUrl)) _settings.ApiBaseUrl = apiBaseUrl.Trim();
        if (!string.IsNullOrWhiteSpace(apiKey)) _settings.ApiKey = apiKey.Trim();
        if (!string.IsNullOrWhiteSpace(sessionId)) _settings.SessionId = sessionId.Trim();

        var result = await _waAkgService.TestConnectionAsync();

        if (!result.Success)
            await _errorLogService.LogAsync("TestConnection", result.Error, result.Raw);

        return Json(new
        {
            success = result.Success,
            httpCode = result.HttpCode,
            message = result.Success
                ? $"Connected. HTTP {result.HttpCode}."
                : result.Error,
            raw = result.Raw?.Length > 800 ? result.Raw[..800] : result.Raw
        });
    }

    /// <summary>Send a single ad-hoc test message.</summary>
    [HttpPost]
    public virtual async Task<IActionResult> SendTest(string phone, string message)
    {
        if (!await HasAccessAsync())
            return Json(new { success = false, message = "Access denied" });

        if (string.IsNullOrWhiteSpace(phone))
            return Json(new { success = false, message = "Enter a phone number" });

        var result = await _waAkgService.SendTextAsync(phone,
            string.IsNullOrWhiteSpace(message) ? "WA-AKG test message from nopCommerce ✅" : message);

        if (!result.Success)
            await _errorLogService.LogAsync("SendTest", result.Error, result.Raw, phone);

        return Json(new
        {
            success = result.Success,
            message = result.Success ? "Message sent." : result.Error
        });
    }

    #endregion

    #region Queue

    /// <summary>Data source of the queue grid (plain JSON, no DataTables dependency).
    /// Plain scalar parameters on purpose: BaseSearchModel's Page/PageSize are meant to be
    /// set by nopCommerce's own grid JS helper, not by a raw form POST, so binding them via
    /// the model here silently stayed at their defaults and every "page" request re-fetched
    /// the same first page while only the on-screen page number advanced.</summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public virtual async Task<IActionResult> QueueList(string searchEventName, int searchStatusId,
        int page = 1, int pageSize = 20)
    {
        if (!await HasAccessAsync())
            return Json(new { error = "Access denied" });

        pageSize = pageSize > 0 ? pageSize : 20;
        var pageIndex = page > 0 ? page - 1 : 0;

        var messages = await _queueService.SearchAsync(searchEventName, searchStatusId,
            pageIndex, pageSize);

        var items = new List<QueuedMessageModel>();
        foreach (var message in messages)
        {
            items.Add(new QueuedMessageModel
            {
                Id = message.Id,
                Phone = message.Phone,
                EventName = message.EventName,
                Status = DescribeStatus(message),
                Retries = message.Retries,
                LastError = message.LastError,
                Message = message.Message?.Length > 120 ? message.Message[..120] + "…" : message.Message,
                CreatedOn = await _dateTimeHelper.ConvertToUserTimeAsync(message.CreatedOnUtc, DateTimeKind.Utc),
                ScheduledOn = await _dateTimeHelper.ConvertToUserTimeAsync(message.ScheduledOnUtc, DateTimeKind.Utc),
                SentOn = message.SentOnUtc.HasValue
                    ? await _dateTimeHelper.ConvertToUserTimeAsync(message.SentOnUtc.Value, DateTimeKind.Utc)
                    : null
            });
        }

        return Json(new { Data = items, Total = messages.TotalCount });
    }

    /// <summary>Reset a row so the background task picks it up again.</summary>
    [HttpPost]
    public virtual async Task<IActionResult> RetryMessage(int id)
    {
        if (!await HasAccessAsync())
            return Json(new { success = false });

        var message = await _queueService.GetByIdAsync(id);
        if (message == null)
            return Json(new { success = false, message = "Not found" });

        message.Retries = 0;
        message.LastError = null;
        message.SentOnUtc = null;
        message.ScheduledOnUtc = await _queueService.ApplyQuietHoursAsync(DateTime.UtcNow);

        await _queueService.UpdateAsync(message);

        return Json(new { success = true });
    }

    /// <summary>Remove a row from the queue.</summary>
    [HttpPost]
    public virtual async Task<IActionResult> DeleteMessage(int id)
    {
        if (!await HasAccessAsync())
            return Json(new { success = false });

        var message = await _queueService.GetByIdAsync(id);
        if (message != null)
            await _queueService.DeleteAsync(message);

        return Json(new { success = true });
    }

    #endregion

    #region Broadcast

    /// <summary>Send or queue a bulk campaign.</summary>
    [HttpPost, ActionName("Configure")]
    [FormValueRequired("broadcast")]
    public virtual async Task<IActionResult> Broadcast(ConfigurationModel model)
    {
        if (!await HasAccessAsync())
            return AccessDeniedView();

        var broadcast = model.Broadcast ?? new BroadcastModel();
        var recipients = ParseRecipients(broadcast.Recipients).ToList();

        if (broadcast.CsvFile is { Length: > 0 })
        {
            using var reader = new StreamReader(broadcast.CsvFile.OpenReadStream());
            recipients.AddRange(ParseRecipients(await reader.ReadToEndAsync()));
        }

        recipients = recipients.Distinct().ToList();

        if (!recipients.Any())
        {
            _notificationService.ErrorNotification("No valid recipients found.");
            return await Configure();
        }

        if (string.IsNullOrWhiteSpace(broadcast.Message))
        {
            _notificationService.ErrorNotification("Message body is empty.");
            return await Configure();
        }

        // an uploaded file takes priority over a typed URL - it is saved under the store's own
        // wwwroot so the gateway (which only accepts media by URL) can fetch it back
        var mediaUrl = broadcast.MediaUrl;
        var mimeType = broadcast.MimeType;

        if (broadcast.MediaFile is { Length: > 0 })
        {
            try
            {
                var (url, resolvedMimeType) = await _mediaUploadService.SaveAsync(broadcast.MediaFile);
                mediaUrl = url;
                mimeType = resolvedMimeType;
            }
            catch (Exception ex)
            {
                await _errorLogService.LogAsync("BroadcastUpload", ex.Message, ex.ToString());
                _notificationService.ErrorNotification(ex.Message);
                return await Configure();
            }
        }

        if (broadcast.UseQueue || !string.IsNullOrWhiteSpace(mediaUrl))
        {
            // queued path: throttled, retried, respects quiet hours and the daily cap.
            // Guard against double-submit (double click / browser resend) re-queuing the same
            // campaign: skip a phone if an unsent row with the same message already exists.
            var alreadyQueued = await _queueService.SearchAsync(WaAkgDefaults.Events.Broadcast, 1, 0, int.MaxValue);
            var pendingKeys = alreadyQueued
                .Where(m => m.Message == broadcast.Message)
                .Select(m => NormalizePhoneKey(m.Phone))
                .ToHashSet();

            var queuedCount = 0;
            var skippedCount = 0;

            foreach (var phone in recipients)
            {
                if (!pendingKeys.Add(NormalizePhoneKey(phone)))
                {
                    skippedCount++;
                    continue;
                }

                await _queueService.EnqueueAsync(phone, broadcast.Message, WaAkgDefaults.Events.Broadcast,
                    mediaUrl, mimeType);
                queuedCount++;
            }

            _notificationService.SuccessNotification(skippedCount > 0
                ? $"{queuedCount} message(s) queued, {skippedCount} skipped (already pending for the same campaign)."
                : $"{queuedCount} message(s) queued. They will be delivered by the background task.");
        }
        else
        {
            var result = await _waAkgService.SendBroadcastAsync(recipients, broadcast.Message, broadcast.DelayMs);

            if (result.Success)
                _notificationService.SuccessNotification($"Broadcast accepted for {recipients.Count} recipient(s).");
            else
            {
                _notificationService.ErrorNotification($"Broadcast failed: {result.Error}");
                await _errorLogService.LogAsync("Broadcast", result.Error, result.Raw);
            }
        }

        return await Configure();
    }

    /// <summary>Delete every still-unsent Broadcast row - lets the admin stop a running campaign
    /// (queued rows not yet picked up by ProcessQueueTask) without touching anything already sent.</summary>
    [HttpPost]
    public virtual async Task<IActionResult> StopBroadcast()
    {
        if (!await HasAccessAsync())
            return Json(new { success = false, message = "Access denied" });

        var pending = await _queueService.SearchAsync(WaAkgDefaults.Events.Broadcast, 1, 0, int.MaxValue);
        var removed = 0;

        foreach (var message in pending)
        {
            await _queueService.DeleteAsync(message);
            removed++;
        }

        return Json(new { success = true, message = $"{removed} pending broadcast message(s) removed." });
    }

    #endregion

    #region Errors

    /// <summary>Data source of the error log grid (plain JSON, same pattern as the queue grid).</summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public virtual async Task<IActionResult> ErrorList(int page = 1, int pageSize = 20)
    {
        if (!await HasAccessAsync())
            return Json(new { error = "Access denied" });

        var pageIndex = page > 0 ? page - 1 : 0;
        var errors = await _errorLogService.GetRecentAsync(pageIndex, pageSize > 0 ? pageSize : 20);

        var items = new List<WaAkgErrorLogModel>();
        foreach (var error in errors)
        {
            items.Add(new WaAkgErrorLogModel
            {
                Id = error.Id,
                Source = error.Source,
                Phone = error.Phone,
                Message = error.Message,
                Detail = error.Detail,
                CreatedOn = await _dateTimeHelper.ConvertToUserTimeAsync(error.CreatedOnUtc, DateTimeKind.Utc)
            });
        }

        return Json(new { Data = items, Total = errors.TotalCount });
    }

    /// <summary>Clear every error log row.</summary>
    [HttpPost]
    public virtual async Task<IActionResult> ClearErrors()
    {
        if (!await HasAccessAsync())
            return Json(new { success = false });

        await _errorLogService.ClearAsync();
        return Json(new { success = true });
    }

    #endregion

    #region Deleted Customers Management

    [HttpPost]
    public virtual async Task<IActionResult> DeletedCustomerList(GridCommand command)
    {
        if (!await HasAccessAsync())
            return Json(new { Success = false, Message = "Access denied." });

        var deletedCustomers = await _deletedCustomerService.SearchAsync(
            pageIndex: command.Page - 1,
            pageSize: command.PageSize
        );

        var gridModel = new GridModel<WaAkgDeletedCustomerModel>
        {
            Data = deletedCustomers.Select(x => new WaAkgDeletedCustomerModel
            {
                Id = x.Id,
                CustomerId = x.CustomerId,
                Email = x.Email,
                Username = x.Username,
                Phone = x.Phone,
                FullName = x.FullName,
                DeletedOnUtc = x.DeletedOnUtc,
                Reason = x.Reason,
                Notes = x.Notes
            }),
            Total = deletedCustomers.TotalCount
        };

        return Json(gridModel);
    }

    [HttpPost]
    public virtual async Task<IActionResult> RecoverDeletedCustomer(int id)
    {
        try 
        {
            if (!await HasAccessAsync())
                return Json(new { Success = false, Message = "Access denied." });

            var result = await _deletedCustomerService.RecoverAsync(id);
            
            if (result)
                return Json(new { Success = true, Message = "Customer recovered successfully." });
            else
                return Json(new { Success = false, Message = "Customer could not be recovered. The original customer record may no longer exist." });
        }
        catch (Exception ex)
        {
            return Json(new { Success = false, Message = ex.Message });
        }
    }

    [HttpPost]
    public virtual async Task<IActionResult> PermanentDeleteCustomer(int id)
    {
        try 
        {
            if (!await HasAccessAsync())
                return Json(new { Success = false, Message = "Access denied." });

            await _deletedCustomerService.PermanentDeleteAsync(id, deleteFromNopCommerce: false);
            return Json(new { Success = true, Message = "Customer permanently deleted." });
        }
        catch (Exception ex)
        {
            return Json(new { Success = false, Message = ex.Message });
        }
    }

    #endregion
}
