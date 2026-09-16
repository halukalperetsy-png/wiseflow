using System.Security.Cryptography;
using CommerceOps.Api.Infrastructure.Configuration;
using CommerceOps.Api.Infrastructure.Persistence;
using CommerceOps.Api.Modules.Catalog.ProductGroups;
using CommerceOps.Api.Modules.Identity.ProductGroupAccess;
using CommerceOps.Api.Modules.Identity.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CommerceOps.IntegrationTests;

/// <summary>
/// A throwaway PostgreSQL 18 container plus a host bound to it, with the small
/// amount of seeding the identity tests need.
///
/// Composition rather than inheritance from WebApplicationFactory, for the same
/// reason as ApiFixture: shutdown order has to be written down explicitly.
/// </summary>
public class IdentityFixture : IAsyncLifetime
{
    private readonly string _databasePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    private readonly PostgreSqlContainer _database;
    private WebApplicationFactory<Program>? _factory;

    public IdentityFixture() =>
        _database = new PostgreSqlBuilder("postgres:18")
            .WithDatabase("commerceops")
            .WithUsername("commerceops")
            .WithPassword(_databasePassword)
            .Build();

    public IServiceProvider Services => _factory!.Services;

    internal string ConnectionString { get; private set; } = null!;

    /// <summary>
    /// Login throttling is a real control with its own test, so it is turned up
    /// out of the way here rather than switched off in the application.
    /// </summary>
    protected virtual IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration =>
    [
        new("Security:LoginRateLimit:PermitLimit", "100000"),

        // Warning keeps the sign-in audit lines (which one test asserts on)
        // while dropping the EF command log that would bury a failure message.
        new("Logging:LogLevel:Default", "Warning"),
        new("Logging:LogLevel:Microsoft", "Warning"),
    ];

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();

        ConnectionString = _database.GetConnectionString();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [$"ConnectionStrings:{ConnectionStringResolver.Name}"] = ConnectionString,
                };

                foreach (var (key, value) in ExtraConfiguration)
                {
                    settings[key] = value;
                }

                configuration.AddInMemoryCollection(settings);
            });
        });

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CommerceOpsDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _database.DisposeAsync();
    }

    /// <summary>A client that keeps cookies, exactly as a browser would.</summary>
    public HttpClient CreateClient()
    {
        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });

        client.Timeout = TimeSpan.FromSeconds(30);

        return client;
    }

    /// <summary>Unique per call: the identity tests share one database.</summary>
    public static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@example.test";

    public static string UniqueCode(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

    public async Task<Guid> CreateUserAsync(
        string email,
        string password,
        bool isActive = true,
        bool mustChangePassword = false,
        params string[] roles)
    {
        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var now = TimeProvider.System.GetUtcNow();

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = email.Split('-')[0],
            IsActive = isActive,
            MustChangePassword = mustChangePassword,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var created = await userManager.CreateAsync(user, password);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Code)));

        if (roles.Length > 0)
        {
            var assigned = await userManager.AddToRolesAsync(user, roles);
            Assert.True(assigned.Succeeded, string.Join(", ", assigned.Errors.Select(error => error.Code)));
        }

        return user.Id;
    }

    public async Task<Guid> CreateProductGroupAsync(string code, string name, bool isActive = true)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CommerceOpsDbContext>();
        var now = TimeProvider.System.GetUtcNow();

        var group = new ProductGroup
        {
            Id = Guid.CreateVersion7(),
            Code = code,
            Name = name,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now,
        };

        context.Set<ProductGroup>().Add(group);
        await context.SaveChangesAsync();

        return group.Id;
    }

    public async Task AssignProductGroupAsync(Guid userId, Guid productGroupId)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CommerceOpsDbContext>();

        context.Set<UserProductGroup>().Add(new UserProductGroup
        {
            UserId = userId,
            ProductGroupId = productGroupId,
            AssignedAt = TimeProvider.System.GetUtcNow(),
        });

        await context.SaveChangesAsync();
    }

    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        await using var scope = Services.CreateAsyncScope();

        return await work(scope.ServiceProvider);
    }
}
