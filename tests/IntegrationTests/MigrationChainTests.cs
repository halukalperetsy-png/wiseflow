using System.Security.Cryptography;
using CommerceOps.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CommerceOps.IntegrationTests;

/// <summary>
/// The migration chain, on an empty database and as an upgrade from the Phase 0
/// database.
///
/// The upgrade case is the one that matters. EF Core builds the
/// __EFMigrationsHistory model from the context's convention set, so a naming
/// convention registered as a convention-set plugin renames that table's columns
/// too. On a fresh database that is invisible -- everything is consistent with
/// itself -- and only an upgrade of an existing database fails, with
/// "column m.migration_id does not exist" before a single migration runs. Snake
/// casing is therefore applied inside OnModelCreating, which the history
/// repository never consults, and these tests are what hold that line.
/// </summary>
public sealed class MigrationChainTests : IAsyncLifetime
{
    // Generated per run, like every other fixture here: no test password is ever
    // committed to source.
    private readonly string _databasePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    private readonly PostgreSqlContainer _database;

    public MigrationChainTests() =>
        _database = new PostgreSqlBuilder("postgres:18")
            .WithDatabase("commerceops")
            .WithUsername("commerceops")
            .WithPassword(_databasePassword)
            .Build();

    private string ConnectionString => _database.GetConnectionString();

    public ValueTask InitializeAsync() => new(_database.StartAsync());

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task The_whole_chain_applies_to_an_empty_database()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);

        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        var expected = context.Database.GetMigrations().ToList();

        Assert.Equal(expected, applied);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));
    }

    /// <summary>
    /// Stop at the Phase 0 migration, then upgrade -- exactly what happens to the
    /// database a developer already has.
    /// </summary>
    [Fact]
    public async Task The_chain_upgrades_a_database_that_stopped_at_the_phase_0_migration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var context = CreateContext();
        var migrations = context.Database.GetMigrations().ToList();
        var phaseZero = migrations[0];

        Assert.EndsWith("InitialCreate", phaseZero, StringComparison.Ordinal);
        Assert.True(migrations.Count > 1, "There is no second migration to upgrade to.");

        await context.Database.GetService<IMigrator>().MigrateAsync(phaseZero, cancellationToken);

        var afterPhaseZero = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        Assert.Equal([phaseZero], afterPhaseZero);

        // The Phase 0 history table is now in place, written by Phase 0's code.
        // This is the step that would fail if the naming pass reached it.
        await context.Database.MigrateAsync(cancellationToken);

        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        Assert.Equal(migrations, applied);
    }

    [Fact]
    public async Task The_migrations_history_table_keeps_the_names_phase_0_created()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);

        var columns = await QueryStringsAsync(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory'
            ORDER BY column_name
            """,
            cancellationToken);

        Assert.Equal(["MigrationId", "ProductVersion"], columns);
    }

    [Fact]
    public async Task Domain_tables_live_in_their_module_schema_with_snake_case_names()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);

        var tables = await QueryStringsAsync(
            """
            SELECT table_schema || '.' || table_name
            FROM information_schema.tables
            WHERE table_schema IN ('identity', 'catalog')
            ORDER BY 1
            """,
            cancellationToken);

        Assert.Equal(
            [
                "catalog.product_groups",
                "identity.role_claims",
                "identity.roles",
                "identity.user_claims",
                "identity.user_logins",
                "identity.user_product_groups",
                "identity.user_roles",
                "identity.user_tokens",
                "identity.users",
            ],
            tables);

        var userColumns = await QueryStringsAsync(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'identity' AND table_name = 'users'
            ORDER BY column_name
            """,
            cancellationToken);

        Assert.Contains("must_change_password", userColumns);
        Assert.Contains("normalized_email", userColumns);
        Assert.Contains("display_name", userColumns);
        Assert.DoesNotContain(userColumns, column => column.Any(char.IsUpper));

        // public holds no domain table; only EF's own history lives there.
        var publicTables = await QueryStringsAsync(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
            ORDER BY table_name
            """,
            cancellationToken);

        Assert.Equal(["__EFMigrationsHistory"], publicTables);
    }

    [Fact]
    public async Task The_seeded_roles_arrive_with_the_migration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);

        var roles = await QueryStringsAsync(
            "SELECT name FROM identity.roles ORDER BY name", cancellationToken);

        Assert.Equal(["Admin", "Viewer"], roles);
    }

    private CommerceOpsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CommerceOpsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    private async Task<List<string>> QueryStringsAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var values = new List<string>();

        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
