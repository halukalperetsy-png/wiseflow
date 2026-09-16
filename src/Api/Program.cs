using CommerceOps.Api.Infrastructure.Configuration;
using CommerceOps.Api.Infrastructure.ErrorHandling;
using CommerceOps.Api.Infrastructure.Persistence;
using CommerceOps.Api.Modules.Catalog.ProductGroups;
using CommerceOps.Api.Modules.Identity.Authentication;
using CommerceOps.Api.Modules.Identity.Authorization;
using CommerceOps.Api.Modules.Identity.Bootstrap;
using CommerceOps.Api.Modules.Identity.Sessions;
using CommerceOps.Api.Modules.Identity.Users;
using CommerceOps.Api.Modules.Platform.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// "bootstrap-admin" is a command, not an endpoint. The verb is removed before
// the host sees the arguments: the command-line configuration provider rejects
// a bare positional argument.
var bootstrapRequested = AdminBootstrapper.IsRequested(args);

var builder = WebApplication.CreateBuilder(AdminBootstrapper.StripVerb(args));

// Resolved from IConfiguration at service-resolution time rather than eagerly,
// so every configuration source is in place first -- including the ones a test
// host adds after the builder is constructed.
builder.Services.AddDbContext<CommerceOpsDbContext>((serviceProvider, options) =>
    options.UseNpgsql(ConnectionStringResolver.Resolve(
        serviceProvider.GetRequiredService<IConfiguration>())));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CommerceOpsDbContext>(name: "database", tags: ["db"]);

// The time limit is scoped to the health check registrations ONLY. The
// connection string is not rewritten and CommandTimeout is not shortened, so
// ordinary DbContext work and EF migrations run unbounded.
builder.Services.Configure<HealthCheckServiceOptions>(options =>
{
    foreach (var registration in options.Registrations)
    {
        registration.Timeout = HealthBudget.CheckTimeout;
    }
});

builder.Services.AddCommerceOpsIdentity(builder.Environment);
builder.Services.AddCommerceOpsAuthorization();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// Fail fast: a missing connection string stops startup with an actionable
// message instead of surfacing on the first request.
ConnectionStringResolver.Resolve(app.Services.GetRequiredService<IConfiguration>());

if (bootstrapRequested)
{
    return await AdminBootstrapper.RunAsync(app.Services);
}

app.UseExceptionHandler();

// Before routing, so it also stamps the 401 and 403 the cookie events write.
app.UseMiddleware<ApiCacheControlMiddleware>();

// Explicit, because the password gate below reads the matched endpoint's
// metadata and therefore has to run after endpoint selection.
app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<PasswordChangeRequiredMiddleware>();

// Vertical slices register themselves explicitly.
app.MapHealthEndpoints();
app.MapSessionEndpoints();
app.MapUserEndpoints();
app.MapProductGroupEndpoints();

app.Run();

return 0;

// Test visibility comes from InternalsVisibleTo; the type stays internal.
internal partial class Program;
