using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Services.Logging;

namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Talks to Nop.Plugin.Payments.CashOnDelivery ("Advanced COD") purely via reflection over its
/// already-loaded assembly, so this project never needs a ProjectReference or a copy of its DLL.
/// If that plugin is not installed, or a future version renames the members this looks for,
/// every call here simply returns "not available" - WA-AKG keeps working without COD amounts.
/// </summary>
public class CodBridgeService : ICodBridgeService
{
    #region Fields

    protected const string CodAssemblyName = "Nop.Plugin.Payments.CashOnDelivery";
    protected const string CodServiceInterfaceName = "Nop.Plugin.Payments.CashOnDelivery.Services.ICodOrderInfoService";

    protected readonly ILogger _logger;
    protected readonly IServiceProvider _serviceProvider;

    // resolved once per app lifetime; -1 = not probed yet, 0 = unavailable, 1 = available
    protected static int _availability = -1;
    protected static Type _serviceInterfaceType;
    protected static MethodInfo _getByOrderIdMethod;
    protected static readonly ConcurrentDictionary<string, PropertyInfo> _propertyCache = new();

    #endregion

    #region Ctor

    public CodBridgeService(ILogger logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    #endregion

    #region Utilities

    /// <summary>Locate the COD plugin's service interface and its GetByOrderIdAsync method, once.</summary>
    protected virtual bool Probe()
    {
        if (_availability != -1)
            return _availability == 1;

        try
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == CodAssemblyName);

            if (assembly == null)
            {
                _availability = 0;
                return false;
            }

            var interfaceType = assembly.GetType(CodServiceInterfaceName);
            var method = interfaceType?.GetMethod("GetByOrderIdAsync", new[] { typeof(int) });

            if (interfaceType == null || method == null)
            {
                _availability = 0;
                return false;
            }

            _serviceInterfaceType = interfaceType;
            _getByOrderIdMethod = method;
            _availability = 1;
            return true;
        }
        catch
        {
            _availability = 0;
            return false;
        }
    }

    /// <summary>Read a property by name via a small reflection cache.</summary>
    protected virtual T ReadProperty<T>(object instance, string propertyName, T fallback = default)
    {
        if (instance == null)
            return fallback;

        var key = $"{instance.GetType().FullName}.{propertyName}";
        var property = _propertyCache.GetOrAdd(key, _ => instance.GetType().GetProperty(propertyName));

        if (property == null)
            return fallback;

        try
        {
            var value = property.GetValue(instance);
            return value is T typed ? typed : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    #endregion

    #region Methods

    /// <summary>True once the Advanced COD plugin's service has been located in this app.</summary>
    public virtual Task<bool> IsAvailableAsync() => Task.FromResult(Probe());

    /// <summary>Fetch the COD breakdown for an order, or null if the plugin is absent or has no row for it.</summary>
    public virtual async Task<CodOrderInfoDto> GetOrderCodInfoAsync(int orderId)
    {
        if (!Probe())
            return null;

        try
        {
            var service = _serviceProvider.GetService(_serviceInterfaceType);
            if (service == null)
                return null;

            var task = (Task)_getByOrderIdMethod.Invoke(service, new object[] { orderId });
            await task.ConfigureAwait(false);

            var resultProperty = task.GetType().GetProperty("Result");
            var codOrderInfo = resultProperty?.GetValue(task);

            if (codOrderInfo == null)
                return null;

            var codTypeId = ReadProperty(codOrderInfo, "CodTypeId", 0);

            return new CodOrderInfoDto
            {
                CodTypeId = codTypeId,
                CodTypeName = codTypeId switch
                {
                    1 => "Without Advance",
                    2 => "Partial Advance",
                    3 => "Full Payment",
                    _ => "Unknown"
                },
                AdvancePaid = ReadProperty<decimal>(codOrderInfo, "AdvancePaid"),
                CodDue = ReadProperty<decimal>(codOrderInfo, "CodDue"),
                ShippingCharge = ReadProperty<decimal>(codOrderInfo, "ShippingCharge"),
                AdvanceAmount = ReadProperty<decimal>(codOrderInfo, "AdvanceAmount"),
                TransactionId = ReadProperty<string>(codOrderInfo, "TransactionId")
            };
        }
        catch (Exception ex)
        {
            await _logger.WarningAsync($"WA-AKG: CodBridgeService lookup failed for order {orderId}: {ex.Message}");
            return null;
        }
    }

    #endregion
}
