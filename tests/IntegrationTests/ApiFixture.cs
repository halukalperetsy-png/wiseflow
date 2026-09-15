using System.Security.Cryptography;
using CommerceOps.Api.Infrastructure.Configuration;
using CommerceOps.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CommerceOps.IntegrationTests;

/// <summary>
/// Owns a throwaway PostgreSQL 18 container and a host bound to it.
///
/// The fixture does NOT derive from WebApplicationFactory. WebApplicationFactory
/// implements IAsyncDisposable itself; deriving and shadowing DisposeAsync makes
/// resources close in the wrong order -- or not at all. Composition lets the
/// shutdown order be written down explicitly.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    // Generated per run. No test password is ever committed to source, and the
    // leak assertion gets a value that cannot match by coincidence.
    private readonly string _databasePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    private readonly PostgreSqlContainer _database;
    private WebApplicationFactory<Program>? _factory;

    public ApiFixture() =>
        _database = new PostgreSqlBuilder("postgres:18")
            .WithDatabase("commerceops")
            .WithUsername("commerceops")
            .WithPassword(_databasePassword)
            .Build();

    public HttpClient Client { get; private set; } = null!;

    public IServiceProvider Services => _factory!.Services;

    /// <summary>The string handed to the host; the isolation test asserts on it.</summary>
    internal string ConnectionString { get; private set; } = null!;

    /// <summary>For the leak assertion only. Never written to disk or output.</summary>
    internal string DatabasePassword => _databasePassword;

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();

        // No shortened timeouts here. The bound lives on the health check
        // registration (see HealthBudget), so tests and the local API share one
        // mechanism while migrations stay unbounded.
        ConnectionString = _database.GetConnectionString();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // NOT Development: user secrets are only added when IsDevelopment(),
            // so a local secrets.json is never read by the tests.
            builder.UseEnvironment("Testing");

            // Added last, therefore highest precedence -- it also wins over an
            // existing ConnectionStrings__CommerceOpsDb environment variable.
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"ConnectionStrings:{ConnectionStringResolver.Name}"] = ConnectionString,
                }));
        });

        Client = _factory.CreateClient();
        Client.Timeout = TimeSpan.FromSeconds(30);

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CommerceOpsDbContext>().Database.MigrateAsync();
    }

    // Reverse order: client first, then the host, and the container last.
    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _database.DisposeAsync();
    }

    /// <summary>Stops the real PostgreSQL server without removing the container.</summary>
    internal Task StopDatabaseAsync() => _database.StopAsync();
}
