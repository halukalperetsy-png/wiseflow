using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommerceOps.Api.Modules.Platform.Health;

internal sealed record HealthCheckResponse(string Name, string Status, double DurationMs);

/// <summary>
/// The wire shape of GET /health.
///
/// Only name, status and duration are projected. HealthReportEntry.Exception,
/// .Description and .Data are deliberately NOT mapped: no connection string,
/// password, exception message or stack trace may ever reach a caller.
/// </summary>
internal sealed record HealthResponse(
    string Status,
    double DurationMs,
    IReadOnlyList<HealthCheckResponse> Checks)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Pure projection -- directly unit testable.</summary>
    internal static HealthResponse From(HealthReport report)
    {
        var checks = report.Entries
            .Select(entry => new HealthCheckResponse(
                entry.Key,
                entry.Value.Status.ToString(),
                ToMilliseconds(entry.Value.Duration)))
            .OrderBy(check => check.Name, StringComparer.Ordinal)
            .ToArray();

        return new HealthResponse(
            report.Status.ToString(),
            ToMilliseconds(report.TotalDuration),
            checks);
    }

    internal static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsJsonAsync(From(report), SerializerOptions);
    }

    private static double ToMilliseconds(TimeSpan duration) =>
        Math.Round(duration.TotalMilliseconds, 1);
}
