namespace CommerceOps.Api.Modules.Identity.Authentication;

/// <summary>
/// Endpoint metadata marking a route as reachable while the signed-in user
/// still owes a password change.
///
/// Exemption is expressed as metadata rather than a path list so that renaming
/// a route cannot silently break the exemption and lock a user out of the very
/// flow that would unlock them.
/// </summary>
internal sealed class AllowPendingPasswordChangeAttribute : Attribute;

internal static class AllowPendingPasswordChangeExtensions
{
    internal static TBuilder AllowPendingPasswordChange<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithMetadata(new AllowPendingPasswordChangeAttribute());
    }
}
