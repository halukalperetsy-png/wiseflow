using System.Security.Claims;
using CommerceOps.Api.Modules.Identity.Authorization;
using CommerceOps.Api.Modules.Identity.Roles;
using CommerceOps.Api.Modules.Identity.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CommerceOps.Api.Modules.Identity.Authentication;

/// <summary>
/// Turns the roles Identity already puts on the principal into permission
/// claims, using the code-held role to permission map.
///
/// The security stamp validator rebuilds the principal through this factory on
/// every request, so a role change is reflected on the next request without the
/// user having to sign in again.
/// </summary>
internal sealed class PermissionClaimsPrincipalFactory(
    UserManager<User> userManager,
    RoleManager<Role> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User, Role>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var identity = await base.GenerateClaimsAsync(user);

        var roleCodes = identity
            .FindAll(Options.ClaimsIdentity.RoleClaimType)
            .Select(claim => claim.Value);

        foreach (var permission in RolePermissions.For(roleCodes))
        {
            identity.AddClaim(new Claim(CommerceOpsClaims.Permission, permission));
        }

        identity.AddClaim(new Claim(CommerceOpsClaims.DisplayName, user.DisplayName));

        if (user.MustChangePassword)
        {
            identity.AddClaim(new Claim(CommerceOpsClaims.MustChangePassword, "true"));
        }

        return identity;
    }
}
