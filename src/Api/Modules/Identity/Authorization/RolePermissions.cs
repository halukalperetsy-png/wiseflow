using CommerceOps.Api.Modules.Identity.Roles;

namespace CommerceOps.Api.Modules.Identity.Authorization;

/// <summary>
/// Which permissions each seeded role carries. Roles are rows (Identity needs
/// them and user_roles refers to them); the mapping to permissions is code.
/// </summary>
internal static class RolePermissions
{
    private static readonly Dictionary<string, string[]> Map = new(StringComparer.Ordinal)
    {
        [RoleCodes.Admin] =
        [
            Permissions.ProductGroupView,
            Permissions.AdminUserManagement,
            Permissions.AdminProductGroupManagement,
        ],
        [RoleCodes.Viewer] =
        [
            Permissions.ProductGroupView,
        ],
    };

    internal static IReadOnlyCollection<string> For(IEnumerable<string> roleCodes)
    {
        ArgumentNullException.ThrowIfNull(roleCodes);

        var permissions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var roleCode in roleCodes)
        {
            if (Map.TryGetValue(roleCode, out var granted))
            {
                permissions.UnionWith(granted);
            }
        }

        return permissions;
    }

    internal static IReadOnlyCollection<string> KnownRoleCodes => Map.Keys;
}
