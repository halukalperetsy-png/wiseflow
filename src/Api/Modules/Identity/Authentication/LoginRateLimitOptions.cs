namespace CommerceOps.Api.Modules.Identity.Authentication;

/// <summary>
/// Login throttling, bound from the "Security:LoginRateLimit" section.
///
/// Bound as options rather than read straight from IConfiguration during
/// registration: a test host layers its configuration in at Build() time, so a
/// value read while services are being registered would silently be the default.
/// </summary>
internal sealed class LoginRateLimitOptions
{
    internal const string SectionName = "Security:LoginRateLimit";

    /// <summary>Requests allowed per window, per client address.</summary>
    public int PermitLimit { get; set; } = 30;

    public int WindowSeconds { get; set; } = 60;
}
