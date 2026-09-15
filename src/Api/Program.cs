using CommerceOps.Api.Infrastructure.Configuration;
using CommerceOps.Api.Infrastructure.ErrorHandling;
using CommerceOps.Api.Infrastructure.Persistence;
using CommerceOps.Api.Modules.Platform.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// Fail fast: a missing connection string stops startup with an actionable
// message instead of surfacing on the first request.
ConnectionStringResolver.Resolve(app.Services.GetRequiredService<IConfiguration>());

app.UseExceptionHandler();

// Vertical slices register themselves explicitly.
app.MapHealthEndpoints();

app.Run();

// Test visibility comes from InternalsVisibleTo; the type stays internal.
internal partial class Program;
