using Nop.Core.Domain.Customers;
using Nop.Plugin.Misc.WaAkg.Services;
using Nop.Services.Customers;
using Nop.Services.Events;
using Nop.Services.Logging;

namespace Nop.Plugin.Misc.WaAkg.Consumers;

/// <summary>
/// Queues the welcome message for a freshly registered customer.
/// </summary>
public class CustomerRegisteredConsumer : IConsumer<CustomerRegisteredEvent>
{
    protected readonly ILogger _logger;
    protected readonly IWaAkgQueueService _queueService;
    protected readonly IWaAkgTokenService _tokenService;
    protected readonly IWaAkgErrorLogService _errorLogService;
    protected readonly WaAkgSettings _settings;

    public CustomerRegisteredConsumer(ILogger logger,
        IWaAkgQueueService queueService,
        IWaAkgTokenService tokenService,
        IWaAkgErrorLogService errorLogService,
        WaAkgSettings settings)
    {
        _logger = logger;
        _queueService = queueService;
        _tokenService = tokenService;
        _errorLogService = errorLogService;
        _settings = settings;
    }

    /// <summary>Handle the event.</summary>
    public async Task HandleEventAsync(CustomerRegisteredEvent eventMessage)
    {
        try
        {
            var customer = eventMessage?.Customer;
            if (customer == null || !_settings.Enabled || !_settings.EnableWelcome)
                return;

            if (!await _queueService.IsOptedInAsync(customer.Id))
                return;

            var phone = await _tokenService.GetCustomerPhoneAsync(customer);
            if (string.IsNullOrWhiteSpace(phone))
                return;

            var message = await _tokenService.BuildForCustomerAsync(_settings.TemplateWelcome, customer);
            await _queueService.EnqueueAsync(phone, message, WaAkgDefaults.Events.Welcome,
                customerId: customer.Id);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("WA-AKG: CustomerRegisteredConsumer failed", ex);
            await _errorLogService.LogAsync("CustomerRegisteredConsumer", ex.Message, ex.ToString());
        }
    }
}
