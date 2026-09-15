using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace CommerceOps.Api.Modules.Platform.Health;

/// <summary>
/// Vertical slice registration convention: each slice exposes one static
/// extension method and Program.cs calls it by name. No IModule interface and
/// no assembly scanning -- the call list in Program.cs is the complete,
/// greppable inventory of what this application exposes.
/// </summary>
internal static class HealthEndpoints
{
    internal static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthResponse.WriteAsync,
        });

        return app;
    }
}
