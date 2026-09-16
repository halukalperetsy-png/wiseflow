using CommerceOps.Api.Modules.Identity.Authorization;
using CommerceOps.Api.Modules.Identity.Roles;

namespace CommerceOps.UnitTests;

/// <summary>
/// The permission catalog is code rather than data, so these keep the catalog,
/// the role map and the seeded roles from drifting apart.
/// </summary>
public sealed class PermissionsCatalogTests
{
    [Fact]
    public void Every_permission_a_role_grants_is_in_the_catalog()
    {
        var granted = RolePermissions.For(RolePermissions.KnownRoleCodes);

        Assert.All(granted, permission => Assert.Contains(permission, Permissions.All));
    }

    [Fact]
    public void Every_catalogued_permission_is_granted_to_at_least_one_role()
    {
        var granted = RolePermissions.For(RolePermissions.KnownRoleCodes);

        Assert.All(Permissions.All, permission => Assert.Contains(permission, granted));
    }

    [Fact]
    public void The_role_map_and_the_seeded_roles_are_the_same_set() =>
        Assert.Equal(
            RoleCodes.All.Order(StringComparer.Ordinal),
            RolePermissions.KnownRoleCodes.Order(StringComparer.Ordinal));

    [Fact]
    public void An_administrator_holds_every_permission() =>
        Assert.Equal(
            Permissions.All.Order(StringComparer.Ordinal),
            RolePermissions.For([RoleCodes.Admin]).Order(StringComparer.Ordinal));

    [Fact]
    public void A_viewer_may_only_view_product_groups() =>
        Assert.Equal([Permissions.ProductGroupView], RolePermissions.For([RoleCodes.Viewer]));

    [Fact]
    public void An_unknown_role_grants_nothing() =>
        Assert.Empty(RolePermissions.For(["SuperAdmin"]));

    [Fact]
    public void No_roles_grant_nothing() =>
        Assert.Empty(RolePermissions.For([]));

    [Fact]
    public void Permissions_are_unique() =>
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct(StringComparer.Ordinal).Count());
}
