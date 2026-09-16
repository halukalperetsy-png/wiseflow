using Microsoft.AspNetCore.Identity;

namespace CommerceOps.Api.Modules.Identity.Users;

/// <summary>
/// The application user. Everything ASP.NET Core Identity needs -- password
/// hash, security stamp, lockout counters, normalised email -- comes from the
/// base type; only the columns this application actually reads are added here.
/// </summary>
internal sealed class User : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }

    /// <summary>
    /// Deactivation is the replacement for deletion. It blocks new sign-ins and,
    /// together with a security stamp change, ends every existing session on the
    /// next request.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Set when an administrator issues a temporary password. While true the
    /// user may only reach the session endpoints and the change-password flow.
    /// </summary>
    public bool MustChangePassword { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
