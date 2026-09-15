using System.Net;
using System.Text.Json;
using CommerceOps.Api.Infrastructure.Configuration;
using CommerceOps.Api.Modules.Platform.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CommerceOps.IntegrationTests;

public sealed class HealthEndpointTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Health_reports_healthy_against_a_real_postgres()
    {
        var response = await fixture.Client.GetAsync("/health", TestContext.Current.CancellationToken);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = JsonSerializer.Deserialize<HealthResponse>(raw, JsonOptions)!;
        Assert.Equal("Healthy", report.Status);

        var database = Assert.Single(report.Checks, check => check.Name == "database");
        Assert.Equal("Healthy", database.Status);
    }

    [Fact]
    public void Host_uses_the_container_connection_string_not_developer_secrets()
    {
        var configuration = fixture.Services.GetRequiredService<IConfiguration>();
        var environment = fixture.Services.GetRequiredService<IHostEnvironment>();

        // Environment is not Development, so the user secrets provider is absent.
        Assert.Equal("Testing", environment.EnvironmentName);

        // Testcontainers assigns a random host port per run, so a match proves
        // the value came from the container and not from a static secrets file.
        Assert.Equal(
            fixture.ConnectionString,
            configuration.GetConnectionString(ConnectionStringResolver.Name));
    }
}
