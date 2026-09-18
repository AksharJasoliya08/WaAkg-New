using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Misc.WaAkg.Components;

/// <summary>
/// Renders a tiny inline script (registered on the standard Footer widget zone, so it appears
/// on every storefront page) that rewrites the theme's mini-cart "Checkout" flyout button so it
/// goes straight to "/checkout" instead of nopCommerce's native "/login/checkoutasguest" page.
///
/// The flyout markup calls a plain client-side redirect —
///   onclick="setLocation('/login/checkoutasguest?returnUrl=%2Fcart')"
/// — which never reaches the server, so WaAkgLoginBypassFilter alone cannot catch it (that
/// filter only sees requests that actually hit the "Customer.Login" action). This script closes
/// that gap on the client, the same way DirectCheckout's own bypass script does for its
/// "Skip Login/Guest Page" setting — except this one is gated on the WhatsApp OTP settings, so
/// it works even when DirectCheckout's setting is left off.
///
/// Only ever renders when the plugin, EnableWhatsAppOtp and BypassNativeLoginForGuestCheckout
/// are all on; otherwise a no-op, same as every other Module B v2 component.
/// </summary>
public class WaAkgLoginBypassScriptViewComponent : NopViewComponent
{
    protected readonly WaAkgSettings _settings;

    public WaAkgLoginBypassScriptViewComponent(WaAkgSettings settings)
    {
        _settings = settings;
    }

    public Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        if (!_settings.Enabled || !_settings.EnableWhatsAppOtp || !_settings.BypassNativeLoginForGuestCheckout)
            return Task.FromResult<IViewComponentResult>(Content(string.Empty));

        return Task.FromResult<IViewComponentResult>(
            View("~/Plugins/Misc.WaAkg/Views/Shared/Components/WaAkgLoginBypassScript/Default.cshtml"));
    }
}
