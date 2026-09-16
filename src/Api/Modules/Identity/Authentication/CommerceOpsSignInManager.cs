using System.Security.Claims;
using CommerceOps.Api.Modules.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CommerceOps.Api.Modules.Identity.Authentication;

/// <summary>
/// Teaches Identity about deactivation, at both ends:
///
/// - CanSignInAsync blocks a new sign-in for an inactive account.
/// - ValidateSecurityStampAsync drops an existing session on its next request.
///
/// Deactivation also rotates the security stamp, so either check alone would be
/// enough; both are here so that flipping is_active directly in SQL still ends
/// the session.
/// </summary>
internal sealed class CommerceOpsSignInManager(
    UserManager<User> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<User> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<User>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<User> confirmation)
    : SignInManager<User>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.IsActive && await base.CanSignInAsync(user);
    }

    public override async Task<User?> ValidateSecurityStampAsync(ClaimsPrincipal? principal)
    {
        var user = await base.ValidateSecurityStampAsync(principal);

        return user is { IsActive: true } ? user : null;
    }
}
