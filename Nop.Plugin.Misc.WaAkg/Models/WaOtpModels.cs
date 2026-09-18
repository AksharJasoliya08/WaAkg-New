namespace Nop.Plugin.Misc.WaAkg.Models;

/// <summary>Body of POST /wa-otp/send.</summary>
public record SendOtpRequestModel
{
    public string Phone { get; set; }
}

/// <summary>Response of POST /wa-otp/send.</summary>
public record SendOtpResponseModel
{
    public bool Success { get; set; }
    public string Error { get; set; }

    /// <summary>Seconds the customer must wait before requesting another OTP (for the resend countdown).</summary>
    public int CooldownSeconds { get; set; }
}

/// <summary>Body of POST /wa-otp/verify.</summary>
public record VerifyOtpRequestModel
{
    public string Phone { get; set; }
    public string Otp { get; set; }
}

/// <summary>Response of POST /wa-otp/verify.</summary>
public record VerifyOtpResponseModel
{
    public bool Success { get; set; }
    public string Error { get; set; }

    /// <summary>True when a wrong OTP just tripped the max-attempts lock (client shows a distinct message).</summary>
    public bool Locked { get; set; }
}
