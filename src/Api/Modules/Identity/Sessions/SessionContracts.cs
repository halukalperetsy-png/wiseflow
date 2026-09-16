namespace CommerceOps.Api.Modules.Identity.Sessions;

/// <summary>The CSRF request token. The matching cookie token is set as HttpOnly.</summary>
internal sealed record CsrfTokenResponse(string Token);

internal sealed record LoginRequest(string? Email, string? Password);

internal sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>
/// What the signed-in user is allowed to know about their own session.
///
/// Product groups are deliberately absent: they are served by
/// GET /api/product-groups, so the scope filter stays on exactly one code path
/// rather than being reimplemented here.
/// </summary>
internal sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool MustChangePassword,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
