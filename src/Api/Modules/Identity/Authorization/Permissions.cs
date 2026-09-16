namespace CommerceOps.Api.Modules.Identity.Authorization;

/// <summary>
/// The permission catalog, in code rather than in a table.
///
/// Phase 1 has no role or permission design screen, so nothing could edit those
/// rows; a permission name has to match its authorization policy name exactly,
/// and compile-time constants make that impossible to misspell and keep "find
/// all references" working. Putting them in data now would be the first step of
/// a permission engine with no consumer.
///
/// Only the permissions Phase 1 actually enforces are declared here.
/// </summary>
internal static class Permissions
{
    internal const string ProductGroupView = "ProductGroup.View";
    internal const string AdminUserManagement = "Admin.UserManagement";
    internal const string AdminProductGroupManagement = "Admin.ProductGroupManagement";

    internal static readonly IReadOnlyList<string> All =
    [
        ProductGroupView,
        AdminUserManagement,
        AdminProductGroupManagement,
    ];
}
