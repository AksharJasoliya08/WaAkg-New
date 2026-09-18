using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.WaAkg;

/// <summary>
/// Settings of the WhatsApp Automation Suite (WA-AKG).
/// </summary>
public class WaAkgSettings : ISettings
{
    #region Connection

    /// <summary>Base URL of the WA-AKG gateway (use the port 80 nginx proxy).</summary>
    public string ApiBaseUrl { get; set; } = "http://66.116.252.225/wa-api";

    /// <summary>Value of the X-API-Key header.</summary>
    public string ApiKey { get; set; } = "wag_3BGv8S6-ng3ndXmSxQtMfJt2_E2lvVau";

    /// <summary>WhatsApp session id (NOT the session name).</summary>
    public string SessionId { get; set; } = "5w7e27";

    /// <summary>Master switch.</summary>
    public bool Enabled { get; set; } = true;

    #endregion

    #region Throttling / anti-ban

    /// <summary>Gap between two queued sends, in seconds.</summary>
    public int SendDelaySeconds { get; set; } = 10;

    /// <summary>Max send attempts per queued message.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Hard cap of messages sent per rolling 24 hours.</summary>
    public int DailySendLimit { get; set; } = 500;

    /// <summary>Quiet window start (store time, 24h). Messages are deferred.</summary>
    public int QuietHourStart { get; set; } = 22;

    /// <summary>Quiet window end (store time, 24h).</summary>
    public int QuietHourEnd { get; set; } = 8;

    /// <summary>Only message customers who opted in.</summary>
    public bool RequireOptIn { get; set; } = true;

    /// <summary>Default state of the checkout opt-in checkbox.</summary>
    public bool OptInCheckedByDefault { get; set; } = true;

    #endregion

    #region Event toggles

    public bool EnableOrderPlaced { get; set; } = true;
    public bool EnableCodAdvance { get; set; } = true;
    public bool EnableOrderPaid { get; set; } = true;
    public bool EnableShipment { get; set; } = true;
    public bool EnableDelivered { get; set; } = true;
    public bool EnableCancelled { get; set; } = true;
    public bool EnableWelcome { get; set; } = true;
    public bool EnableCartReminder { get; set; } = true;

    #endregion

    #region Templates

    public string TemplateOrderPlaced { get; set; } =
        "✅ *ORDER CONFIRMED!*\n\nHi %CustomerName%,\nYour order *%OrderNumber%* of *%Total%* is confirmed.\n\n🛍️ *Items:*\n%OrderItems%\n\n🎁 Code *NEW10* = 10% OFF next order\n🎀 Free gift on Partial Pay / Prepaid!\n\nOrder details: %TrackUrl%\n— THE MALAS JEWELRY 💎";

    /// <summary>Sent only when the customer clicks "Pay Advance" from the order-details page -
    /// never automatically at order placement.</summary>
    public string TemplateCodAdvance { get; set; } =
        "📦 *Order %OrderNumber% (COD)*\n\nTo confirm, pay a small advance *₹60*:\n%PaymentLink%\n\n🎀 Pay now & get a FREE GIFT with your order!\n\nOrder details: %TrackUrl%\n— THE MALAS 💎";

    /// <summary>Sent when the advance is actually paid (superseding any still-pending CodAdvance nudge).</summary>
    public string TemplateCodAdvancePaid { get; set; } =
        "✅ Advance paid from order details - Order %OrderNumber%. Thank you %CustomerName%! 🎀 Your free gift is added.\n\nOrder details: %TrackUrl%";

    public string TemplateOrderPaid { get; set; } =
        "💰 *PAYMENT RECEIVED!*\nOrder %OrderNumber% — %Total%. Thank you %CustomerName%! 🎀 Your free gift is added.";

    public string TemplateShipment { get; set; } =
        "🚚 *SHIPPED!*\nOrder %OrderNumber% is on the way.\nCarrier: %Carrier%\nTracking: %TrackingNumber%\n\nOrder details: %TrackUrl%";

    public string TemplateDelivered { get; set; } =
        "✅ *DELIVERED!*\nHope you love it %CustomerName%! 😍\nRate your purchase: %ProductUrl%";

    public string TemplateCancelled { get; set; } =
        "❌ *ORDER CANCELLED*\nOrder %OrderNumber%. Refund (if paid) in 5-7 days.";

    public string TemplateWelcome { get; set; } =
        "💎 Welcome to *THE MALAS JEWELRY*, %CustomerName%!\nUse *NEW10* for 10% OFF your first order: %StoreUrl%";

    /// <summary>Text sent alongside each cart item's own image (one message per item).</summary>
    public string TemplateCartReminder { get; set; } =
        "🛒 You left this behind, %CustomerName%!\n%CartItems%\nComplete your order now with code *NEW10* (10% OFF): %CartUrl%";

    /// <summary>COD due reminder, sent alongside OrderPlaced/OrderPaid when a COD balance remains.</summary>
    public string TemplateCodDue { get; set; } =
        "💳 *Payment summary — Order %OrderNumber%*\n\nOrder total: *%Total%*\nAlready paid: *%AdvancePaid%*\n👉 *To pay on delivery (COD): %CodDue%*\n\nThank you for shopping with THE MALAS 💎";

    #endregion

    #region Owner / admin notifications

    /// <summary>Master switch for the owner/admin copy of order notifications.</summary>
    public bool OwnerNotificationsEnabled { get; set; } = false;

    /// <summary>Phone number(s) that receive owner notifications. Comma separated for more than one.</summary>
    public string OwnerNotificationNumbers { get; set; } = string.Empty;

    /// <summary>Owner template for a new order (any payment method).</summary>
    public string TemplateOwnerOrderPlaced { get; set; } =
        "🛎️ *NEW ORDER* #%OrderNumber%\nCustomer: %CustomerName% (%CustomerPhone%)\nTotal: *%Total%*\nPayment: %PaymentMethod%\n%CodSummary%\n🔗 %AdminOrderUrl%";

    /// <summary>Owner template for a COD order specifically (adds the due-on-delivery amount).</summary>
    public string TemplateOwnerCod { get; set; } =
        "💰 *COD ORDER* #%OrderNumber%\nCustomer: %CustomerName% (%CustomerPhone%)\nOrder total: %Total%\nAlready paid: %AdvancePaid%\n*COD to collect: %CodDue%*\n🔗 %AdminOrderUrl%";

    #endregion

    #region Misc

    /// <summary>Coupon pushed inside the cart reminder.</summary>
    public string CartReminderCoupon { get; set; } = "NEW10";

    /// <summary>Cart must be idle this many hours before a reminder is queued.</summary>
    public int CartReminderHours { get; set; } = 12;

    /// <summary>Value of the %PaymentLink% token.</summary>
    public string PaymentLink { get; set; } = "https://themalas.com/uploaded/Payment.png";

    /// <summary>
    /// Pattern used to build %TrackUrl% - the customer-facing order details page, not the
    /// storefront homepage. {0} = store URL, {1} = numeric order id. Default matches the
    /// standard nopCommerce theme route (/orderdetails/{orderId}).
    /// </summary>
    public string OrderTrackUrlPattern { get; set; } = "{0}orderdetails/{1}";

    /// <summary>Default country calling code prefixed to 10-digit numbers.</summary>
    public string DefaultCountryCode { get; set; } = "91";

    /// <summary>
    /// Orders paid via the COD plugin need a short delay before the COD amounts are queried,
    /// since that plugin's own OrderPlacedEvent consumer may not have finished writing its
    /// CodOrderInfo row yet when WA-AKG's own consumer runs (event order is not guaranteed).
    /// </summary>
    public int CodInfoLookupDelaySeconds { get; set; } = 90;

    #endregion

    #region WhatsApp OTP Login (Module B v2)

    /// <summary>Master switch for the WhatsApp OTP checkout gate popup.</summary>
    public bool EnableWhatsAppOtp { get; set; } = false;

    /// <summary>OTP WhatsApp message. Tokens: %OTP%, %ExpiryMin%.</summary>
    public string OtpMessageTemplate { get; set; } =
        "🔐 *THE MALAS JEWELRY*\nLogin OTP: *%OTP%*\n⏳ %ExpiryMin% min valid. Do not share.";

    /// <summary>Temp password WhatsApp message, sent right after auto-registration. Tokens: %PASSWORD%, %ProfileUrl%, %StoreUrl%.</summary>
    public string OtpPasswordTemplate { get; set; } =
        "👤 Account created at THE MALAS!\nYour login: your WhatsApp number\nTemp password: *%PASSWORD%*\nPlease change it here: %ProfileUrl%\nShop: %StoreUrl%";

    /// <summary>Minutes an OTP stays valid.</summary>
    public int OtpExpiryMinutes { get; set; } = 5;

    /// <summary>Wrong-OTP attempts allowed before the code is locked out.</summary>
    public int OtpMaxAttempts { get; set; } = 3;

    /// <summary>Seconds a phone number must wait before it can request another OTP.</summary>
    public int OtpResendCooldownSec { get; set; } = 45;

    /// <summary>
    /// When true (and EnableWhatsAppOtp is also true), a guest who would otherwise land on
    /// nopCommerce's native "/login" (Login/Register or "checkout as guest") page — from the
    /// cart page's Checkout button, the mini-cart's Checkout link, or any other link pointing
    /// at that page during the checkout flow — is sent straight to "/checkout" instead, where
    /// the WhatsApp OTP modal takes over. This is independent of DirectCheckout's own
    /// "Skip Login/Guest Page" setting; leave that plugin's setting off and control the
    /// behavior from here so only the WhatsApp OTP flow (not a generic guest-checkout bypass)
    /// replaces the native login page.
    /// </summary>
    public bool BypassNativeLoginForGuestCheckout { get; set; } = true;

    #endregion
}
