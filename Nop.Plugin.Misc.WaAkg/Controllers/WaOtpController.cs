using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Data;
using Nop.Plugin.Misc.WaAkg.Models;
using Nop.Plugin.Misc.WaAkg.Services;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Web.Framework.Controllers;
using IAuthenticationService = Nop.Services.Authentication.IAuthenticationService;

namespace Nop.Plugin.Misc.WaAkg.Controllers;

/// <summary>
/// Storefront endpoint powering the WhatsApp OTP checkout gate modal (Module B):
/// send an OTP over WhatsApp, verify it, and on success either sign in an existing customer
/// (found by their Phone number - never re-registered) or auto-register a brand new one.
/// </summary>
public class WaOtpController : BasePluginController
{
    #region Fields

    protected readonly IWorkContext _workContext;
    protected readonly IStoreContext _storeContext;
    protected readonly IWebHelper _webHelper;
    protected readonly ICustomerService _customerService;
    protected readonly ICustomerRegistrationService _customerRegistrationService;
    protected readonly IAuthenticationService _authenticationService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly IShoppingCartService _shoppingCartService;
    protected readonly IRepository<Customer> _customerRepository;
    protected readonly IWaAkgService _waAkgService;
    protected readonly IWaAkgErrorLogService _errorLogService;
    protected readonly IMemoryCache _memoryCache;
    protected readonly ILogger _logger;
    protected readonly CustomerSettings _customerSettings;
    protected readonly WaAkgSettings _settings;

    #endregion

    #region Ctor

    public WaOtpController(IWorkContext workContext,
        IStoreContext storeContext,
        IWebHelper webHelper,
        ICustomerService customerService,
        ICustomerRegistrationService customerRegistrationService,
        IAuthenticationService authenticationService,
        IGenericAttributeService genericAttributeService,
        IShoppingCartService shoppingCartService,
        IRepository<Customer> customerRepository,
        IWaAkgService waAkgService,
        IWaAkgErrorLogService errorLogService,
        IMemoryCache memoryCache,
        ILogger logger,
        CustomerSettings customerSettings,
        WaAkgSettings settings)
    {
        _workContext = workContext;
        _storeContext = storeContext;
        _webHelper = webHelper;
        _customerService = customerService;
        _customerRegistrationService = customerRegistrationService;
        _authenticationService = authenticationService;
        _genericAttributeService = genericAttributeService;
        _shoppingCartService = shoppingCartService;
        _customerRepository = customerRepository;
        _waAkgService = waAkgService;
        _errorLogService = errorLogService;
        _memoryCache = memoryCache;
        _logger = logger;
        _customerSettings = customerSettings;
        _settings = settings;
    }

    #endregion

    #region Utilities

    /// <summary>Digits-only phone, matching the normalization WaAkgService.ToJid applies (10-digit -> prefixed with the default country code).</summary>
    protected virtual string NormalizePhone(string phone)
    {
        var digits = Regex.Replace(phone ?? string.Empty, @"\D", string.Empty);

        if (digits.Length == 10)
            digits = $"{_settings.DefaultCountryCode}{digits}";
        else if (digits.Length == 11 && digits.StartsWith("0"))
            digits = $"{_settings.DefaultCountryCode}{digits[1..]}";

        return digits;
    }

    protected virtual string OtpCacheKey(string phone) => string.Format(WaAkgDefaults.OtpCodeCacheKey, phone);
    protected virtual string ResendCacheKey(string phone) => string.Format(WaAkgDefaults.OtpResendCacheKey, phone);
    protected virtual string AttemptsCacheKey(string phone) => string.Format(WaAkgDefaults.OtpAttemptsCacheKey, phone);

    /// <summary>Cryptographically random 6-digit numeric OTP.</summary>
    protected virtual string GenerateOtp()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }

    /// <summary>Cryptographically random 12-char temp password containing upper, lower and digit characters.</summary>
    protected virtual string GenerateTempPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;

        var chars = new char[12];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];

        for (var i = 3; i < chars.Length; i++)
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    /// <summary>
    /// Find an existing customer by their WhatsApp phone number. Checks, in order:
    /// 1) the native Customer.Phone field (the one shown/edited on the account profile page and
    ///    the source of truth going forward for every account this flow creates or touches),
    /// 2) the legacy synthetic-email pattern from earlier plugin versions, for accounts created
    ///    before this field was populated, so nobody already registered gets a duplicate account.
    /// Deleted customers are correctly treated as not found - they were removed on purpose and
    /// the next OTP verify for that number is a fresh registration, not an error.
    /// </summary>
    protected virtual async Task<Customer> FindExistingCustomerByPhoneAsync(string phone)
    {
        var byPhone = await LinqToDB.AsyncExtensions.FirstOrDefaultAsync(
            _customerRepository.Table
                .Where(c => !c.Deleted && c.Phone == phone)
                .OrderBy(c => c.Id)
        );

        if (byPhone != null)
            return byPhone;

        // legacy lookup: accounts created by older plugin versions under the phone-derived
        // email/username pattern, before Customer.Phone itself was being set
        var legacyLookupUsername = _customerSettings.UsernamesEnabled ? phone : $"{phone}@{WaAkgDefaults.OtpAccountEmailDomain}";
        var legacy = await _customerService.GetCustomerByUsernameAsync(legacyLookupUsername);
        legacy ??= await _customerService.GetCustomerByEmailAsync($"{phone}@{WaAkgDefaults.OtpAccountEmailDomain}");

        return legacy;
    }

    #endregion

    #region Actions

    /// <summary>Generate and WhatsApp an OTP to the given phone number, subject to a resend cooldown.</summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public virtual async Task<IActionResult> SendOtp([FromBody] SendOtpRequestModel model)
    {
        if (!_settings.Enabled || !_settings.EnableWhatsAppOtp)
            return Json(new SendOtpResponseModel { Success = false, Error = "OTP login is not enabled." });

        var phone = NormalizePhone(model?.Phone);
        if (phone.Length < 10)
            return Json(new SendOtpResponseModel { Success = false, Error = "Enter a valid phone number." });

        var cooldownSeconds = Math.Max(1, _settings.OtpResendCooldownSec);

        if (_memoryCache.TryGetValue(ResendCacheKey(phone), out DateTimeOffset lockedUntil) &&
            lockedUntil > DateTimeOffset.UtcNow)
        {
            var remaining = (int)Math.Ceiling((lockedUntil - DateTimeOffset.UtcNow).TotalSeconds);
            return Json(new SendOtpResponseModel
            {
                Success = false,
                Error = $"Please wait {remaining}s before requesting another OTP.",
                CooldownSeconds = remaining
            });
        }

        var otp = GenerateOtp();
        var expiryMinutes = Math.Max(1, _settings.OtpExpiryMinutes);

        _memoryCache.Set(OtpCacheKey(phone), otp, TimeSpan.FromMinutes(expiryMinutes));
        _memoryCache.Remove(AttemptsCacheKey(phone));
        _memoryCache.Set(ResendCacheKey(phone), DateTimeOffset.UtcNow.AddSeconds(cooldownSeconds),
            TimeSpan.FromSeconds(cooldownSeconds));

        var message = (_settings.OtpMessageTemplate ?? string.Empty)
            .Replace("%OTP%", otp)
            .Replace("%ExpiryMin%", expiryMinutes.ToString());

        var result = await _waAkgService.SendTextAsync(phone, message);

        if (!result.Success)
        {
            await _errorLogService.LogAsync("WaOtpController.SendOtp", result.Error, result.Raw, phone);
            _memoryCache.Remove(OtpCacheKey(phone));
            _memoryCache.Remove(ResendCacheKey(phone));
            return Json(new SendOtpResponseModel
            {
                Success = false,
                Error = "Could not send the OTP right now. Please try again in a moment."
            });
        }

        return Json(new SendOtpResponseModel { Success = true, CooldownSeconds = cooldownSeconds });
    }

    /// <summary>Verify the OTP; on success, sign in an existing customer found by phone, or
    /// auto-register a brand new one if none exists.</summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public virtual async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequestModel model)
    {
        if (!_settings.Enabled || !_settings.EnableWhatsAppOtp)
            return Json(new VerifyOtpResponseModel { Success = false, Error = "OTP login is not enabled." });

        var phone = NormalizePhone(model?.Phone);
        var otpEntered = (model?.Otp ?? string.Empty).Trim();

        if (phone.Length < 10 || otpEntered.Length != 6)
            return Json(new VerifyOtpResponseModel { Success = false, Error = "Invalid request." });

        var maxAttempts = Math.Max(1, _settings.OtpMaxAttempts);
        var attempts = _memoryCache.TryGetValue(AttemptsCacheKey(phone), out int currentAttempts) ? currentAttempts : 0;

        if (attempts >= maxAttempts)
            return Json(new VerifyOtpResponseModel { Success = false, Locked = true, Error = "Too many attempts. Please request a new OTP." });

        if (!_memoryCache.TryGetValue(OtpCacheKey(phone), out string storedOtp) || string.IsNullOrEmpty(storedOtp))
            return Json(new VerifyOtpResponseModel { Success = false, Error = "OTP expired. Please request a new one." });

        if (!string.Equals(storedOtp, otpEntered, StringComparison.Ordinal))
        {
            attempts++;
            _memoryCache.Set(AttemptsCacheKey(phone), attempts, TimeSpan.FromMinutes(Math.Max(1, _settings.OtpExpiryMinutes)));

            var attemptsLeft = maxAttempts - attempts;
            if (attemptsLeft <= 0)
            {
                _memoryCache.Remove(OtpCacheKey(phone));
                return Json(new VerifyOtpResponseModel { Success = false, Locked = true, Error = "Too many attempts. Please request a new OTP." });
            }

            return Json(new VerifyOtpResponseModel { Success = false, Error = $"Incorrect OTP. {attemptsLeft} attempt(s) left." });
        }

        // Correct OTP - consume it so it can never be reused, then resolve/create the customer.
        _memoryCache.Remove(OtpCacheKey(phone));
        _memoryCache.Remove(AttemptsCacheKey(phone));
        _memoryCache.Remove(ResendCacheKey(phone));

        try
        {
            var customer = await FindExistingCustomerByPhoneAsync(phone);

            if (customer == null)
            {
                var store = await _storeContext.GetCurrentStoreAsync();
                var currentCustomer = await _workContext.GetCurrentCustomerAsync();

                // Reuse the current (guest) customer record as the one being registered, exactly
                // like nopCommerce's own registration flow does, so the cart already attached to
                // this session/cookie carries straight through into the now-authenticated checkout.
                customer = currentCustomer;

                var tempPassword = GenerateTempPassword();

                // Hidden, random, never-shown email - the customer logs in with their phone
                // number (as the username) and the OTP/temp password, never this address.
                // The full 32-hex-char guid is kept intact (never truncated) so two different
                // phone numbers can never collide onto the same hidden email.
                var hiddenEmail = $"wa_{phone}_{Guid.NewGuid():N}@{WaAkgDefaults.OtpAccountEmailDomain}";

                var lookupUsername = _customerSettings.UsernamesEnabled ? phone : hiddenEmail;

                var registrationRequest = new CustomerRegistrationRequest(
                    customer,
                    hiddenEmail,
                    lookupUsername,
                    tempPassword,
                    PasswordFormat.Hashed,
                    store.Id,
                    true);

                var registrationResult = await _customerRegistrationService.RegisterCustomerAsync(registrationRequest);

                if (!registrationResult.Success)
                {
                    var errorText = string.Join("; ", registrationResult.Errors);
                    await _errorLogService.LogAsync("WaOtpController.VerifyOtp.Register", errorText, null, phone);
                    return Json(new VerifyOtpResponseModel { Success = false, Error = "Could not create your account. Please try again." });
                }

                // Native Phone field (shown/edited on the account profile page) and the plugin's
                // own attribute - both kept in sync so future OTP logins find this account by
                // Customer.Phone directly, and the profile page shows the right number immediately.
                customer.Phone = phone;
                await _customerService.UpdateCustomerAsync(customer);

                await _genericAttributeService.SaveAttributeAsync(customer, WaAkgDefaults.OtpPhoneAttribute, phone);
                await _genericAttributeService.SaveAttributeAsync(customer, WaAkgDefaults.MustChangePasswordAttribute, true);

                // No address is created here on purpose - BillingAddressId stays null and the
                // customer fills their own real address on nopCommerce's native, empty checkout
                // billing-address form, exactly like any other new customer.

                var storeUrl = _webHelper.GetStoreLocation().TrimEnd('/');
                var passwordMessage = (_settings.OtpPasswordTemplate ?? string.Empty)
                    .Replace("%PASSWORD%", tempPassword)
                    .Replace("%ProfileUrl%", $"{storeUrl}/customer/info")
                    .Replace("%StoreUrl%", storeUrl);

                var sendResult = await _waAkgService.SendTextAsync(phone, passwordMessage);
                if (!sendResult.Success)
                    await _errorLogService.LogAsync("WaOtpController.VerifyOtp.SendPassword", sendResult.Error, sendResult.Raw, phone);
            }
            else
            {
                // Returning customer: keep the native Phone field and the plugin attribute in
                // sync even for accounts that predate this field being set (legacy lookup match).
                if (customer.Phone != phone)
                {
                    customer.Phone = phone;
                    await _customerService.UpdateCustomerAsync(customer);
                }

                await _genericAttributeService.SaveAttributeAsync(customer, WaAkgDefaults.OtpPhoneAttribute, phone);

                // Carry over whatever the guest added to their cart on this device/session
                // before verifying, into the returning customer's own cart - nopCommerce's own
                // native login flow does the same merge (ShoppingCartService.MigrateShoppingCart)
                // so items picked as a guest are never silently lost on login.
                var guestCustomer = await _workContext.GetCurrentCustomerAsync();
                if (guestCustomer.Id != customer.Id)
                    await _shoppingCartService.MigrateShoppingCartAsync(guestCustomer, customer, true);
            }

            await _authenticationService.SignInAsync(customer, true);
            await _workContext.SetCurrentCustomerAsync(customer);

            return Json(new VerifyOtpResponseModel { Success = true });
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("WA-AKG: WaOtpController.VerifyOtp failed", ex);
            await _errorLogService.LogAsync("WaOtpController.VerifyOtp", ex.Message, ex.ToString(), phone);
            return Json(new VerifyOtpResponseModel { Success = false, Error = "Something went wrong. Please try again." });
        }
    }

    #endregion
}
