using CommerceOps.Api.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommerceOps.Api.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` build a DbContext without starting the web host.
///
/// The configuration chain is declared explicitly and is independent of the
/// working directory: AppContext.BaseDirectory points at the build output, and
/// user secrets resolve through the assembly's UserSecretsId attribute. The
/// documented EF commands therefore work when run from the repository root.
///
/// No shortened timeouts are applied here -- migrations may legitimately take
/// longer than a health check.
/// </summary>
internal sealed class CommerceOpsDbContextFactory : IDesignTimeDbContextFactory<CommerceOpsDbContext>
{
    public CommerceOpsDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<CommerceOpsDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<CommerceOpsDbContext>()
            .UseNpgsql(ConnectionStringResolver.Resolve(configuration))
            .Options;

        return new CommerceOpsDbContext(options);
    }
}
