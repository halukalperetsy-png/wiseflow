using System.Diagnostics;
using System.Net;
using System.Text.Json;
using CommerceOps.Api.Modules.Platform.Health;

namespace CommerceOps.IntegrationTests;

/// <summary>
/// Gets its own ApiFixture instance (IClassFixture is per test class), so
/// stopping the database here cannot affect the healthy-path tests.
/// </summary>
public sealed class HealthEndpointDatabaseDownTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Health_returns_503_with_a_valid_body_and_leaks_nothing_when_postgres_is_unreachable()
    {
        await fixture.StopDatabaseAsync();

        var stopwatch = Stopwatch.StartNew();
        var response = await fixture.Client.GetAsync("/health", TestContext.Current.CancellationToken);
        stopwatch.Stop();
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // 1) The API is alive and says so with the right status code.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        // 2) The body is still valid and names the failing dependency.
        var report = JsonSerializer.Deserialize<HealthResponse>(raw, JsonOptions)!;
        Assert.Equal("Unhealthy", report.Status);

        var database = Assert.Single(report.Checks, check => check.Name == "database");
        Assert.Equal("Unhealthy", database.Status);

        // 3) Time budget: the frontend gives up at 3s, so the API must answer first.
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(3),
            $"/health took {stopwatch.Elapsed.TotalSeconds:F1}s; the frontend timeout is 3s.");

        // 4) No credential, connection string or exception detail may escape.
        Assert.DoesNotContain(fixture.DatabasePassword, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("Host=", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", raw, StringComparison.Ordinal);
    }
}
