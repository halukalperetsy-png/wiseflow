using Microsoft.AspNetCore.Identity;

namespace CommerceOps.Api.Modules.Identity.Authentication;

/// <summary>
/// Turns Identity's error codes into the field-keyed Turkish messages the API
/// returns. Unknown codes fall back to a generic message rather than leaking
/// framework wording.
/// </summary>
internal static class IdentityErrorTranslator
{
    internal static Dictionary<string, string[]> ToValidationErrors(
        IEnumerable<IdentityError> errors,
        string defaultField)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var byField = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var error in errors)
        {
            var (field, message) = Translate(error, defaultField);

            if (!byField.TryGetValue(field, out var messages))
            {
                messages = [];
                byField[field] = messages;
            }

            if (!messages.Contains(message, StringComparer.Ordinal))
            {
                messages.Add(message);
            }
        }

        return byField.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }

    private static (string Field, string Message) Translate(IdentityError error, string defaultField) =>
        error.Code switch
        {
            "PasswordMismatch" => ("currentPassword", "Mevcut parolanız hatalı."),
            "PasswordTooShort" => ("newPassword", PasswordRules.Requirement),
            "PasswordRequiresUniqueChars" => ("newPassword", PasswordRules.Requirement),
            "PasswordRequiresDigit" or "PasswordRequiresLower"
                or "PasswordRequiresUpper" or "PasswordRequiresNonAlphanumeric"
                => ("newPassword", PasswordRules.Requirement),
            "DuplicateEmail" or "DuplicateUserName" => ("email", "Bu e-posta adresi zaten kayıtlı."),
            "InvalidEmail" => ("email", "Geçerli bir e-posta adresi girin."),
            _ => (defaultField, "İşlem tamamlanamadı. Girdiğiniz bilgileri kontrol edin."),
        };
}
