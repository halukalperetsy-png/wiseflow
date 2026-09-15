using System.Text.Json;
using CommerceOps.Api.Modules.Platform.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommerceOps.UnitTests;

public sealed class HealthResponseTests
{
    private const string SecretValue = "super-secret-password-value";

    private static HealthReport ReportWith(params (string Name, HealthStatus Status)[] entries)
    {
        var dictionary = entries.ToDictionary(
            entry => entry.Name,
            entry => new HealthReportEntry(
                entry.Status,
                description: $"Host=127.0.0.1;Password={SecretValue}",
                duration: TimeSpan.FromMilliseconds(12.345),
                exception: new InvalidOperationException($"Npgsql failed using {SecretValue}"),
                data: new Dictionary<string, object> { ["connectionString"] = SecretValue }),
            StringComparer.Ordinal);

        return new HealthReport(dictionary, TimeSpan.FromMilliseconds(34.567));
    }

    [Fact]
    public void From_projects_status_duration_and_checks()
    {
        var response = HealthResponse.From(ReportWith(("database", HealthStatus.Healthy)));

        Assert.Equal("Healthy", response.Status);
        Assert.Equal(34.6, response.DurationMs);

        var check = Assert.Single(response.Checks);
        Assert.Equal("database", check.Name);
        Assert.Equal("Healthy", check.Status);
        Assert.Equal(12.3, check.DurationMs);
    }

    [Fact]
    public void From_reports_unhealthy_status()
    {
        var response = HealthResponse.From(ReportWith(("database", HealthStatus.Unhealthy)));

        Assert.Equal("Unhealthy", response.Status);
        Assert.Equal("Unhealthy", Assert.Single(response.Checks).Status);
    }

    [Fact]
    public void From_orders_checks_by_name()
    {
        var response = HealthResponse.From(ReportWith(
            ("storage", HealthStatus.Healthy),
            ("database", HealthStatus.Healthy)));

        Assert.Equal(["database", "storage"], response.Checks.Select(check => check.Name));
    }

    [Fact]
    public void Serialized_response_never_leaks_description_exception_or_data()
    {
        var response = HealthResponse.From(ReportWith(("database", HealthStatus.Unhealthy)));

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain(SecretValue, json, StringComparison.Ordinal);
        Assert.DoesNotContain("Host=", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialized_response_uses_the_camelCase_contract_the_frontend_expects()
    {
        var response = HealthResponse.From(ReportWith(("database", HealthStatus.Healthy)));

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"status\":", json, StringComparison.Ordinal);
        Assert.Contains("\"durationMs\":", json, StringComparison.Ordinal);
        Assert.Contains("\"checks\":", json, StringComparison.Ordinal);
        Assert.Contains("\"name\":", json, StringComparison.Ordinal);
    }
}
