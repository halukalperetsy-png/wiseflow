namespace CommerceOps.Api.Modules.Identity.Authentication;

/// <summary>
/// The password policy, in one place, so the Identity option and the message
/// shown to the user cannot disagree.
/// </summary>
internal static class PasswordRules
{
    internal const int MinimumLength = 12;
    internal const int MaximumLength = 256;

    internal static string Requirement =>
        $"Parola en az {MinimumLength} karakter olmalıdır.";
}
