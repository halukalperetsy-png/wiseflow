namespace CommerceOps.Api.Modules.Identity.Bootstrap;

/// <summary>The first administrator's details. Never sourced from the repository.</summary>
internal sealed record BootstrapAdminSettings(string Email, string DisplayName, string Password);

/// <summary>
/// Resolves the first administrator's details, following the same pattern as
/// ConnectionStringResolver: read from configuration, fail with a message that
/// names the exact command to run, and never print the value itself.
///
/// There is no default password anywhere in this repository, and creating an
/// administrator is not reachable over HTTP.
/// </summary>
internal static class BootstrapSettingsResolver
{
    internal const string EmailKey = "Bootstrap:AdminEmail";
    internal const string DisplayNameKey = "Bootstrap:AdminDisplayName";
    internal const string PasswordKey = "Bootstrap:AdminPassword";

    internal static string MissingMessage(IEnumerable<string> missingKeys) =>
        "The first administrator is not configured. Missing: "
        + string.Join(", ", missingKeys)
        + ". Set the values outside the repository with:"
        + $"\n  dotnet user-secrets set \"{EmailKey}\" \"<address>\" --project src/Api"
        + $"\n  dotnet user-secrets set \"{DisplayNameKey}\" \"<full name>\" --project src/Api"
        + $"\n  dotnet user-secrets set \"{PasswordKey}\" \"<password>\" --project src/Api"
        + "\nThen run: dotnet run --project src/Api -- bootstrap-admin";

    internal static BootstrapAdminSettings Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var email = configuration[EmailKey];
        var displayName = configuration[DisplayNameKey];
        var password = configuration[PasswordKey];

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(email))
        {
            missing.Add(EmailKey);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            missing.Add(DisplayNameKey);
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            missing.Add(PasswordKey);
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(MissingMessage(missing));
        }

        return new BootstrapAdminSettings(email!.Trim(), displayName!.Trim(), password!);
    }
}
