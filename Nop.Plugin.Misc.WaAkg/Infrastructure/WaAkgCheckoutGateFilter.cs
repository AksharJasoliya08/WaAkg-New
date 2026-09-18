using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Services.Customers;

namespace Nop.Plugin.Misc.WaAkg.Infrastructure;

/// <summary>
/// Server-side backstop for the WhatsApp OTP checkout gate (Module B v2).
///
/// The popup itself is what stops a guest from proceeding in the normal UI flow, but a guest
/// could still POST straight to a Checkout action (curl, browser devtools, a stale tab) and
/// skip the modal entirely. This filter closes that gap: any POST into the "Checkout"
/// controller by a guest customer is rejected with 403 JSON while WhatsApp OTP login is
/// enabled. GET requests are always allowed through so the checkout pages (and the OTP modal
/// widget rendered on top of them) can still render.
/// </summary>
public class WaAkgCheckoutGateFilter : IAsyncActionFilter
{
    protected readonly IWorkContext _workContext;
    protected readonly ICustomerService _customerService;
    protected readonly WaAkgSettings _settings;

    public WaAkgCheckoutGateFilter(IWorkContext workContext,
        ICustomerService customerService,
        WaAkgSettings settings)
    {
        _workContext = workContext;
        _customerService = customerService;
        _settings = settings;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!_settings.Enabled || !_settings.EnableWhatsAppOtp)
        {
            await next();
            return;
        }

        var request = context.HttpContext.Request;
        if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var controller = context.RouteData.Values["controller"]?.ToString();
        if (!string.Equals(controller, "Checkout", StringComparison.OrdinalIgnoreCase))
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

        context.Result = new JsonResult(new { error = "OTP login required" })
        {
            StatusCode = 403
        };
    }
}
