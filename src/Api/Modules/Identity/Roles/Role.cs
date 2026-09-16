using Microsoft.AspNetCore.Identity;

namespace CommerceOps.Api.Modules.Identity.Roles;

/// <summary>
/// A role. Identity's <c>Name</c> is the stable code the permission map keys on
/// (see RolePermissions); <see cref="DisplayName"/> is the label the admin UI
/// renders, so adding a role in a later phase needs no frontend change.
/// </summary>
internal sealed class Role : IdentityRole<Guid>
{
    public required string DisplayName { get; set; }
}
