using CommerceOps.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace CommerceOps.UnitTests;

public sealed class ConnectionStringResolverTests
{
    private static IConfiguration ConfigurationWith(string? connectionString)
    {
        var values = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{ConnectionStringResolver.Name}"] = connectionString,
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void Resolve_returns_the_configured_value()
    {
        const string Expected = "Host=127.0.0.1;Port=5432;Database=commerceops";

        Assert.Equal(Expected, ConnectionStringResolver.Resolve(ConfigurationWith(Expected)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_fails_fast_when_the_value_is_missing_or_blank(string? connectionString)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ConnectionStringResolver.Resolve(ConfigurationWith(connectionString)));

        Assert.Equal(ConnectionStringResolver.MissingMessage, exception.Message);
    }

    [Fact]
    public void Missing_message_tells_the_developer_exactly_what_to_run()
    {
        var message = ConnectionStringResolver.MissingMessage;

        Assert.Contains("dotnet user-secrets set", message, StringComparison.Ordinal);
        Assert.Contains(ConnectionStringResolver.Name, message, StringComparison.Ordinal);
        Assert.Contains("--project src/Api", message, StringComparison.Ordinal);
    }
}
