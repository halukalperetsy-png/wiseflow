namespace CommerceOps.Api.Modules.Identity.Authorization;

/// <summary>Claim types this application adds on top of Identity's own.</summary>
internal static class CommerceOpsClaims
{
    internal const string Permission = "permission";
    internal const string DisplayName = "display_name";
    internal const string MustChangePassword = "must_change_password";
}
