using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Services.Customers;

namespace Nop.Plugin.Misc.WaAkg.Infrastructure;

/// <summary>
/// Server-side redirect that hands checkout login over to the WhatsApp OTP popup instead of
/// nopCommerce's native Login/Register page (Module B v2 — login bypass).
///
/// Without this filter, a guest who reaches checkout while DirectCheckout's own
/// "Skip Login/Guest Page" setting is OFF follows nopCommerce's normal flow: clicking
/// "Checkout" (cart page button or mini-cart link) sends them to nopCommerce's native
/// "/login/checkoutasguest" page, and the WaOtpWidget never gets a chance to render because it
/// only ever fires on "/checkout" itself.
///
/// This filter fires on GET requests whose path is "/login/checkoutasguest" - nopCommerce's own
/// named route ("LoginCheckoutAsGuest") for the guest-checkout variant of the Login page; that
/// route segment, not a query string, is what carries the "checkout as guest" signal. For a
/// guest customer, it 302-redirects straight to "/checkout" instead of letting the native login
/// view render. The WhatsApp OTP modal (WaOtpWidget) then takes over on that page exactly as it
/// already does for every other checkout entry point.
///
/// This is completely independent of DirectCheckout's "Skip Login/Guest Page" setting — that
/// setting can stay off (so DirectCheckout's own generic bypass never fires), while this filter
/// still swaps ONLY the login page specifically for the WhatsApp OTP flow. Turned off by either
/// the plugin master switch, EnableWhatsAppOtp, or its own
/// BypassNativeLoginForGuestCheckout setting, this is a complete no-op.
/// </summary>
public class WaAkgLoginBypassFilter : IAsyncActionFilter
{
    protected readonly IWorkContext _workContext;
    protected readonly ICustomerService _customerService;
    protected readonly WaAkgSettings _settings;

    public WaAkgLoginBypassFilter(IWorkContext workContext,
        ICustomerService customerService,
        WaAkgSettings settings)
    {
        _workContext = workContext;
        _customerService = customerService;
        _settings = settings;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!_settings.Enabled || !_settings.EnableWhatsAppOtp || !_settings.BypassNativeLoginForGuestCheckout)
        {
            await next();
            return;
        }

        var request = context.HttpContext.Request;
        if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        // The "checkout as guest" login page is reached via nopCommerce's own named route
        // "LoginCheckoutAsGuest", whose URL pattern is "login/checkoutasguest" -
        // "checkoutasguest" is a path segment baked into the URL itself (there is no
        // "?checkoutasguest=true" query string anywhere). Both ShoppingCartController's guest
        // POST redirect and the mini-cart's own link ultimately resolve to that route, and it
        // maps to the same "Customer.Login" action as the plain "/login" page - just reached via
        // a different URL pattern - so keying on the path itself (rather than trying to read the
        // route values a second, less reliable way) is the one signal guaranteed to match
        // regardless of exactly how that named route binds its parameters. A guest who instead
        // opens the plain header "Log in" link (path "/login", no "checkoutasguest" segment)
        // still sees the normal native login page, matching acceptance test "Header /login page
        // -> no OTP modal" for the widget.
        var path = (request.Path.Value ?? string.Empty).TrimEnd('/');
        if (!path.EndsWith("/login/checkoutasguest", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var customer = await _workContext.GetCurrentCustomerAsync();
        var isGuest = await _customerService.IsGuestAsync(customer);

        if (!isGuest)
        {
            await next();
            return;
        }

        // Plain root-relative path (no "~" tilde prefix): nopCommerce's own
        // NopRedirectResultExecutor builds a Uri directly from this string rather than
        // resolving Razor's "~/" virtual-path syntax first (that resolution only happens via
        // Controller.Redirect()/Url.Content(), not a bare "new RedirectResult(...)" from inside
        // an action filter), so "~/checkout" throws "Invalid URI: The hostname could not be
        // parsed" here. "/checkout" is a normal root-relative URL and parses correctly.
        // "/checkout" is nopCommerce's stable, long-standing checkout entry point across
        // versions, and the WaOtpWidget component (registered on "body_start_html_tag_after")
        // already renders the modal for exactly this path.
        context.Result = new RedirectResult("/checkout");
    }
}
