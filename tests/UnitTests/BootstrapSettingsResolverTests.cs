using CommerceOps.Api.Modules.Identity.Bootstrap;
using Microsoft.Extensions.Configuration;

namespace CommerceOps.UnitTests;

/// <summary>
/// The first administrator comes from local configuration and nowhere else. A
/// missing value has to name the exact command that fixes it -- the same
/// contract ConnectionStringResolver follows.
/// </summary>
public sealed class BootstrapSettingsResolverTests
{
    private const string Email = "someone@example.test";
    private const string DisplayName = "Someone";
    private const string Secret = "not-a-real-password";

    [Fact]
    public void All_three_values_present_resolves()
    {
        var settings = BootstrapSettingsResolver.Resolve(ConfigurationWith(Email, DisplayName, Secret));

        Assert.Equal(Email, settings.Email);
        Assert.Equal(DisplayName, settings.DisplayName);
        Assert.Equal(Secret, settings.Password);
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed_from_the_identity_values()
    {
        var settings = BootstrapSettingsResolver.Resolve(
            ConfigurationWith($"  {Email}  ", $"  {DisplayName}  ", Secret));

        Assert.Equal(Email, settings.Email);
        Assert.Equal(DisplayName, settings.DisplayName);
    }

    [Theory]
    [InlineData(null, DisplayName, Secret, BootstrapSettingsResolver.EmailKey)]
    [InlineData("", DisplayName, Secret, BootstrapSettingsResolver.EmailKey)]
    [InlineData("   ", DisplayName, Secret, BootstrapSettingsResolver.EmailKey)]
    [InlineData(Email, null, Secret, BootstrapSettingsResolver.DisplayNameKey)]
    [InlineData(Email, DisplayName, null, BootstrapSettingsResolver.PasswordKey)]
    [InlineData(Email, DisplayName, "   ", BootstrapSettingsResolver.PasswordKey)]
    public void A_missing_value_stops_with_a_message_naming_that_key(
        string? email,
        string? displayName,
        string? password,
        string expectedKey)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => BootstrapSettingsResolver.Resolve(ConfigurationWith(email, displayName, password)));

        Assert.Contains(expectedKey, exception.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet user-secrets set", exception.Message, StringComparison.Ordinal);
        Assert.Contains("bootstrap-admin", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_message_never_repeats_a_configured_value()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => BootstrapSettingsResolver.Resolve(ConfigurationWith(Email, DisplayName, password: null)));

        Assert.DoesNotContain(Email, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(DisplayName, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_null_configuration_is_rejected() =>
        Assert.Throws<ArgumentNullException>(() => BootstrapSettingsResolver.Resolve(null!));

    private static IConfiguration ConfigurationWith(string? email, string? displayName, string? password) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [BootstrapSettingsResolver.EmailKey] = email,
                [BootstrapSettingsResolver.DisplayNameKey] = displayName,
                [BootstrapSettingsResolver.PasswordKey] = password,
            })
            .Build();
}
