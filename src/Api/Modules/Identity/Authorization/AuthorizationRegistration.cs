namespace CommerceOps.Api.Modules.Identity.Authorization;

/// <summary>
/// One authorization policy per permission, generated from the catalog so a
/// policy can never be referenced by a name that does not exist.
///
/// No scheme is named: Identity's application cookie is the default
/// authentication scheme, so the policies pick it up.
/// </summary>
internal static class AuthorizationRegistration
{
    internal static IServiceCollection AddCommerceOpsAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddAuthorizationBuilder();

        foreach (var permission in Permissions.All)
        {
            builder.AddPolicy(permission, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(CommerceOpsClaims.Permission, permission));
        }

        return services;
    }
}
