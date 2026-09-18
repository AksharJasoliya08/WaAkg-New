using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Customers;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Misc.WaAkg.Components;

/// <summary>
/// Renders the WhatsApp OTP checkout gate modal (Module B v2) — but ONLY when all of the
/// following hold, otherwise it renders nothing:
///   • the plugin and the WhatsApp OTP setting are both enabled;
///   • the current request path is "/checkout" or a "/checkout/..." sub-step (billing address,
///     shipping, payment, confirm, etc — every step of the native checkout wizard, and the
///     one-page checkout route used by BSS OnePageCheckout);
///   • the current customer is a guest.
///
/// This widget is registered on "body_start_html_tag_after", a zone that renders right after
/// the opening &lt;body&gt; tag on EVERY storefront page — so this path check is the ONLY thing
/// keeping it off /cart, product pages, the homepage, and the native /login page. Reaching /checkout in the first place already
/// means the guest clicked "Checkout" from the cart or the mini-cart and was carried here by
/// the DirectCheckout plugin's guest-page bypass (or nopCommerce's normal checkout flow if that
/// plugin/setting is off) — so gating strictly on this path is exactly "OTP appears when the
/// guest hits Checkout, nowhere else".
/// </summary>
public class WaOtpWidgetViewComponent : NopViewComponent
{
    protected readonly IWorkContext _workContext;
    protected readonly ICustomerService _customerService;
    protected readonly WaAkgSettings _settings;

    public WaOtpWidgetViewComponent(IWorkContext workContext,
        ICustomerService customerService,
        WaAkgSettings settings)
    {
        _workContext = workContext;
        _customerService = customerService;
        _settings = settings;
    }

    /// <summary>True for "/checkout" itself and any "/checkout/xyz" sub-path, and likewise for
    /// "/onepagecheckout" and "/onepagecheckout/xyz" (the BSS OnePageCheckout plugin's route,
    /// which this store's checkout actually resolves to even though the browser's address bar
    /// still shows "/checkout"). False for anything else (including "/checkoutattributes",
    /// "/checkoutasguest" etc — those are different routes that merely start with the same
    /// letters, not checkout steps).</summary>
    protected virtual bool IsCheckoutPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // strip a trailing slash so "/checkout/" and "/checkout" match the same way
        path = path.TrimEnd('/');

        return path.Equals("/checkout", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/checkout/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/onepagecheckout", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/onepagecheckout/", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        if (!_settings.Enabled)
            return Content(string.Empty);
        if (!_settings.EnableWhatsAppOtp)
            return Content(string.Empty);

        var path = HttpContext.Request.Path.Value ?? string.Empty;
        if (!IsCheckoutPath(path))
            return Content(string.Empty);

        var customer = await _workContext.GetCurrentCustomerAsync();
        var isGuest = await _customerService.IsGuestAsync(customer);
        if (!isGuest)
            return Content(string.Empty);

        return View("~/Plugins/Misc.WaAkg/Views/Shared/Components/WaOtpWidget/Default.cshtml", _settings.OtpResendCooldownSec);
    }
}
