namespace CommerceOps.Api.Modules.Identity.Roles;

/// <summary>
/// The roles Phase 1 seeds. The design document names four more
/// (ProductResearcher, ContentEditor, MarketplaceOperator, Approver); under the
/// Phase 1 permission set they would all behave exactly like Viewer, so they
/// arrive with the Product.* permissions in Phase 2 rather than as
/// indistinguishable options in the role picker.
/// </summary>
internal static class RoleCodes
{
    internal const string Admin = "Admin";
    internal const string Viewer = "Viewer";

    internal static readonly IReadOnlyList<string> All = [Admin, Viewer];
}
