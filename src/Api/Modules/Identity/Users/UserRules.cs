namespace CommerceOps.Api.Modules.Identity.Users;

/// <summary>Input rules shared by the entity configuration and the user slices.</summary>
internal static class UserRules
{
    internal const int DisplayNameMaxLength = 200;
    internal const int DisplayNameMinLength = 2;
    internal const int EmailMaxLength = 256;

    internal static string DisplayNameRequirement =>
        $"Ad {DisplayNameMinLength}-{DisplayNameMaxLength} karakter olmalıdır.";
}
