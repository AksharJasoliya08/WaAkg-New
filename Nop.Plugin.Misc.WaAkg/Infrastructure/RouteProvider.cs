using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.WaAkg.Infrastructure;

/// <summary>
/// Friendly routes of the plugin.
/// </summary>
public class RouteProvider : IRouteProvider
{
    /// <summary>Register routes.</summary>
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        // storefront endpoint used by the checkout opt-in checkbox
        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.Misc.WaAkg.SaveOptIn",
            pattern: "wa-akg/save-opt-in",
            defaults: new { controller = "WaAkgPublic", action = "SaveOptIn" });

        // admin shortcuts
        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.Misc.WaAkg.Configure",
            pattern: "Admin/WaAkgAdmin/Configure",
            defaults: new { controller = "WaAkgAdmin", action = "Configure", area = "Admin" });

        // WhatsApp OTP checkout gate (Module B v2)
        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.Misc.WaAkg.WaOtp.Send",
            pattern: "wa-otp/send",
            defaults: new { controller = "WaOtp", action = "SendOtp" });

        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.Misc.WaAkg.WaOtp.Verify",
            pattern: "wa-otp/verify",
            defaults: new { controller = "WaOtp", action = "VerifyOtp" });
    }

    /// <summary>Priority of the route provider.</summary>
    public int Priority => 0;
}
