using System.Security.Claims;
using CommerceOps.Api.Infrastructure.Persistence;
using CommerceOps.Api.Modules.Catalog.ProductGroups;
using CommerceOps.Api.Modules.Identity.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CommerceOps.Api.Modules.Identity.ProductGroupAccess;

/// <summary>
/// The one product group data filter. Listing, reading a single record and
/// changing one all go through it, so the three can never drift apart -- which
/// is exactly how a scope leak usually starts.
///
/// A permission check alone is never sufficient (module-conventions.md): the
/// policy decides whether a caller may perform the operation, this decides
/// which rows the operation may touch.
/// </summary>
internal static class ProductGroupScope
{
    internal static IQueryable<ProductGroup> Visible(CommerceOpsDbContext context, ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(principal);

        var groups = context.Set<ProductGroup>();

        // Administrators manage every group, including retired ones.
        if (principal.HasPermission(Permissions.AdminProductGroupManagement))
        {
            return groups;
        }

        var userId = principal.GetRequiredUserId();

        return groups.Where(group => group.IsActive
            && context.Set<UserProductGroup>()
                .Any(assignment => assignment.UserId == userId && assignment.ProductGroupId == group.Id));
    }

    /// <summary>
    /// Out-of-scope reads answer 404 rather than 403: a user who may not see a
    /// group should not learn that it exists.
    /// </summary>
    internal static Task<ProductGroup?> FindVisibleAsync(
        CommerceOpsDbContext context,
        ClaimsPrincipal principal,
        Guid id,
        CancellationToken cancellationToken) =>
        Visible(context, principal).FirstOrDefaultAsync(group => group.Id == id, cancellationToken);
}
