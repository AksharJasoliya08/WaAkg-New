using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Common;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Misc.WaAkg.Components;

/// <summary>
/// Renders the "Get order updates on WhatsApp" checkbox inside the checkout.
/// </summary>
public class WaAkgOptInViewComponent : NopViewComponent
{
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly IWorkContext _workContext;
    protected readonly WaAkgSettings _settings;

    public WaAkgOptInViewComponent(IGenericAttributeService genericAttributeService,
        IWorkContext workContext,
        WaAkgSettings settings)
    {
        _genericAttributeService = genericAttributeService;
        _workContext = workContext;
        _settings = settings;
    }

    /// <summary>Invoke the view component.</summary>
    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        if (!_settings.Enabled)
            return Content(string.Empty);

        var customer = await _workContext.GetCurrentCustomerAsync();
        var stored = await _genericAttributeService.GetAttributeAsync<string>(customer, WaAkgDefaults.OptInAttribute);

        var isChecked = string.IsNullOrEmpty(stored)
            ? _settings.OptInCheckedByDefault
            : stored.Equals("true", StringComparison.OrdinalIgnoreCase);

        return View("~/Plugins/Misc.WaAkg/Views/Shared/Components/WaAkgOptIn/Default.cshtml", isChecked);
    }
}
