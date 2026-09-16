using System.Security.Claims;

namespace CommerceOps.Api.Modules.Identity.Authorization;

/// <summary>
/// Reads the signed-in user off the request principal. There is no new service:
/// the principal is rebuilt from the database on every request by the security
/// stamp validator, so these values are never stale.
///
/// The identifier is taken from here and never from a request body -- that is
/// the rule that stops a caller from acting as somebody else.
/// </summary>
internal static class CurrentUserExtensions
{
    internal static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    internal static Guid GetRequiredUserId(this ClaimsPrincipal principal) =>
        principal.GetUserId()
        ?? throw new InvalidOperationException("The request principal carries no user identifier.");

    internal static bool HasPermission(this ClaimsPrincipal principal, string permission)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.HasClaim(CommerceOpsClaims.Permission, permission);
    }

    internal static bool MustChangePassword(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.HasClaim(CommerceOpsClaims.MustChangePassword, "true");
    }

    internal static string GetDisplayName(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirstValue(CommerceOpsClaims.DisplayName) ?? string.Empty;
    }

    internal static IReadOnlyList<string> GetRoleCodes(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return [.. principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Order(StringComparer.Ordinal)];
    }

    internal static IReadOnlyList<string> GetPermissions(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return [.. principal.FindAll(CommerceOpsClaims.Permission).Select(claim => claim.Value).Order(StringComparer.Ordinal)];
    }
}
