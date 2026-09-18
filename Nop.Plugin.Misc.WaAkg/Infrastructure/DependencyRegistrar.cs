using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.WaAkg.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;

namespace Nop.Plugin.Misc.WaAkg.Infrastructure;

/// <summary>
/// Registers plugin services and the named HttpClient used by the gateway client.
/// </summary>
//public class DependencyRegistrar : IDependencyRegistrar
//{
//    /// <summary>Register services and interfaces.</summary>
//    public virtual void Register(IServiceCollection services, ITypeFinder typeFinder, AppSettings appSettings)
//    {
//        services.AddScoped<IWaAkgService, WaAkgService>();
//        services.AddScoped<IWaAkgQueueService, WaAkgQueueService>();
//        services.AddScoped<IWaAkgTokenService, WaAkgTokenService>();


//    }

//    /// <summary>Order of this registrar implementation.</summary>
//    public int Order => 10;
//}

public class DependencyRegistrar : INopStartup
{
    /// <summary>
    /// Add and configure any of the middleware
    /// </summary>
    /// <param name="services">Collection of service descriptors</param>
    /// <param name="configuration">Configuration of the application</param>
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {


        services.AddScoped<IWaAkgService, WaAkgService>();
        services.AddScoped<IWaAkgQueueService, WaAkgQueueService>();
        services.AddScoped<IWaAkgTokenService, WaAkgTokenService>();
        services.AddScoped<IWaAkgErrorLogService, WaAkgErrorLogService>();
        services.AddScoped<ICodBridgeService, CodBridgeService>();
        services.AddScoped<IWaAkgMediaUploadService, WaAkgMediaUploadService>();

        services.AddHttpClient(WaAkgDefaults.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(25);
            client.DefaultRequestHeaders.Add("User-Agent", "nopCommerce-WaAkg/1.0");
        });

        // WhatsApp OTP checkout gate (Module B v2) - global action filters. Registered here
        // (not as attributes on core controllers) so they run for every request and are a
        // complete no-op the instant EnableWhatsAppOtp is off in settings.
        services.AddTransient<WaAkgCheckoutGateFilter>();
        services.AddTransient<WaAkgMustChangeFilter>();

        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<WaAkgCheckoutGateFilter>();
            options.Filters.Add<WaAkgMustChangeFilter>();
            options.Filters.Add<WaAkgLoginBypassFilter>();
        });

        services.AddTransient<WaAkgLoginBypassFilter>();
    }

    /// <summary>
    /// Configure the using of added middleware
    /// </summary>
    /// <param name="application">Builder for configuring an application's request pipeline</param>
    public void Configure(IApplicationBuilder application)
    {
        // Self-healing task registration: guarantees the purge task exists even on a site where
        // the plugin was installed before this task was introduced (InstallAsync only runs once,
        // on first install, so an already-installed site would otherwise never get it).
        // EnsureScheduleTask is idempotent - safe to call on every app start.
        try
        {
            using var scope = application.ApplicationServices.CreateScope();
            var scheduleTaskService = scope.ServiceProvider.GetService<Nop.Services.ScheduleTasks.IScheduleTaskService>();
            if (scheduleTaskService != null)
            {
                var existing = scheduleTaskService.GetTaskByTypeAsync(WaAkgDefaults.PurgeStaleQueueTaskType).GetAwaiter().GetResult();
                if (existing == null)
                {
                    scheduleTaskService.InsertTaskAsync(new Nop.Core.Domain.ScheduleTasks.ScheduleTask
                    {
                        Name = WaAkgDefaults.PurgeStaleQueueTaskName,
                        Type = WaAkgDefaults.PurgeStaleQueueTaskType,
                        Seconds = 24 * 60 * 60,
                        Enabled = true,
                        StopOnError = false
                    }).GetAwaiter().GetResult();
                }
            }
        }
        catch
        {
            // best-effort only - the plugin's Configure page still works even if this fails,
            // and InstallAsync already covers a fresh install of the plugin
        }

        // Self-healing settings/locale upgrade: sites that installed the plugin before the
        // login-bypass feature existed would otherwise have BypassNativeLoginForGuestCheckout
        // sitting at the C# default (false) in memory, with nothing in the DB ever forcing the
        // intended default (true) or adding the admin-page resource strings. This block is
        // idempotent (SaveSettingAsync/AddOrUpdateLocaleResourceAsync are safe to call every
        // app start) and — like the task registration above — is a best-effort, non-fatal step.
        try
        {
            using var scope = application.ApplicationServices.CreateScope();
            var settingService = scope.ServiceProvider.GetService<ISettingService>();
            var localizationService = scope.ServiceProvider.GetService<ILocalizationService>();

            if (settingService != null)
            {
                var settings = settingService.LoadSettingAsync<WaAkgSettings>().GetAwaiter().GetResult();
                if (!settingService.SettingExistsAsync(settings, s => s.BypassNativeLoginForGuestCheckout)
                        .GetAwaiter().GetResult())
                {
                    settings.BypassNativeLoginForGuestCheckout = true;
                    settingService.SaveSettingAsync(settings).GetAwaiter().GetResult();
                }
            }

            localizationService?.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Misc.WaAkg.Fields.BypassNativeLoginForGuestCheckout"] = "Bypass native login page for guest checkout",
                ["Plugins.Misc.WaAkg.Fields.BypassNativeLoginForGuestCheckout.Hint"] = "When a guest would normally be sent to nopCommerce's native Login/Register page (Checkout button on the cart page or mini-cart, with \"Skip Login/Guest Page\" off in DirectCheckout), send them straight to /checkout instead so the WhatsApp OTP popup handles login/registration. Has no effect unless \"Enable WhatsApp OTP checkout login\" above is also on."
            }).GetAwaiter().GetResult();
        }
        catch
        {
            // best-effort only
        }
    }

    /// <summary>
    /// Gets order of this startup configuration implementation
    /// </summary>
    public int Order => 3000;
}