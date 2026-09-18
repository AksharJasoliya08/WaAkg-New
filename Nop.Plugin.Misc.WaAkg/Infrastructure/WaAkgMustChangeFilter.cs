using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Services.Common;
using Nop.Services.Customers;

namespace Nop.Plugin.Misc.WaAkg.Infrastructure;

/// <summary>
/// After a WhatsApp OTP auto-registration, the customer is signed in with a random temp
/// password and MUST change it before doing anything else in the account area — EXCEPT
/// finishing the checkout/cart flow they were already in the middle of, which must never be
/// interrupted by a forced redirect.
///
/// Fires only for authenticated customers with the pending flag set, and only redirects away
/// from the Customer area (excluding password-change/logout themselves). Cart, checkout and
/// every storefront page outside "Customer" are left completely untouched.
/// </summary>
public class WaAkgMustChangeFilter : IAsyncActionFilter
{
    protected readonly IWorkContext _workContext;
    protected readonly ICustomerService _customerService;
    protected readonly IGenericAttributeService _genericAttributeService;

    public WaAkgMustChangeFilter(IWorkContext workContext,
        ICustomerService customerService,
        IGenericAttributeService genericAttributeService)
    {
        _workContext = workContext;
        _customerService = customerService;
        _genericAttributeService = genericAttributeService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = context.RouteData.Values["controller"]?.ToString();
        var action = context.RouteData.Values["action"]?.ToString();

        // Only ever acts inside the native "Customer" area (My Account pages). Cart, Checkout,
        // and everything else on the storefront is never touched by this filter.
        if (!string.Equals(controller, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        // Never block the password-change page itself or logout — that would make the flag
        // impossible to clear.
        if (!string.IsNullOrEmpty(action) &&
            (action.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(action, "Logout", StringComparison.OrdinalIgnoreCase)))
        {
            await next();
            return;
        }

        var customer = await _workContext.GetCurrentCustomerAsync();
        var isGuest = await _customerService.IsGuestAsync(customer);
        if (isGuest)
        {
            await next();
            return;
        }

        var mustChange = await _genericAttributeService
            .GetAttributeAsync<bool>(customer, WaAkgDefaults.MustChangePasswordAttribute);

        if (!mustChange)
        {
            await next();
            return;
        }

        // Plain root-relative path (no "~" tilde prefix): nopCommerce's own
        // NopRedirectResultExecutor builds a Uri directly from this string rather than
        // resolving Razor's "~/" virtual-path syntax first (that only happens via
        // Controller.Redirect()/Url.Content(), not a bare "new RedirectResult(...)" from an
        // action filter), so "~/customer/changepassword" throws "Invalid URI: The hostname
        // could not be parsed" here. "/customer/changepassword" is nopCommerce's stable,
        // long-standing native change-password path and parses correctly as a plain
        // root-relative URL.
        context.Result = new RedirectResult("/customer/changepassword");
    }
}
