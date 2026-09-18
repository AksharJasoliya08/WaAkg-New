namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Optional bridge to the "Payments.CashOnDelivery" (Advanced COD) plugin, if installed.
/// Every method degrades to "not available" instead of throwing when that plugin is absent,
/// disabled, or its schema does not match what this bridge expects.
/// </summary>
public interface ICodBridgeService
{
    /// <summary>True once the Advanced COD plugin's service has been located in this app.</summary>
    Task<bool> IsAvailableAsync();

    /// <summary>Fetch the COD breakdown for an order, or null if the plugin is absent or has no row for it.</summary>
    Task<CodOrderInfoDto> GetOrderCodInfoAsync(int orderId);
}
