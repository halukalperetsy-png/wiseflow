using System.Text.RegularExpressions;

namespace CommerceOps.Api.Modules.Catalog.ProductGroups;

/// <summary>
/// Input rules shared by the create and update slices and by the entity
/// configuration, so the database constraint and the validation message can
/// never drift apart.
/// </summary>
internal static partial class ProductGroupRules
{
    internal const int CodeMaxLength = 50;
    internal const int CodeMinLength = 2;
    internal const int NameMaxLength = 200;
    internal const int DescriptionMaxLength = 1000;

    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]*$")]
    private static partial Regex CodePattern { get; }

    /// <summary>Upper-cases with the invariant culture: this machine runs a Turkish locale.</summary>
    internal static string NormalizeCode(string code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant();

    internal static bool IsValidCode(string normalizedCode) =>
        normalizedCode.Length >= CodeMinLength
        && normalizedCode.Length <= CodeMaxLength
        && CodePattern.IsMatch(normalizedCode);
}
