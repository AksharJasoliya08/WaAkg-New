using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Common;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Misc.WaAkg.Controllers;

/// <summary>
/// Storefront endpoint that stores the checkout WhatsApp opt-in choice.
/// </summary>
public class WaAkgPublicController : BasePluginController
{
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly IWorkContext _workContext;

    public WaAkgPublicController(IGenericAttributeService genericAttributeService,
        IWorkContext workContext)
    {
        _genericAttributeService = genericAttributeService;
        _workContext = workContext;
    }

    /// <summary>Persist the opt-in flag on the current customer.</summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public virtual async Task<IActionResult> SaveOptIn(bool optIn)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        await _genericAttributeService.SaveAttributeAsync(customer, WaAkgDefaults.OptInAttribute,
            optIn ? "true" : "false");

        return Json(new { success = true, optIn });
    }
}
