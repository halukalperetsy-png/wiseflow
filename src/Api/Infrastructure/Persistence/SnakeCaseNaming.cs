using System.Text;
using Microsoft.EntityFrameworkCore;

namespace CommerceOps.Api.Infrastructure.Persistence;

/// <summary>
/// Rewrites every table, column, key, foreign key and index name in the model
/// to snake_case, so PostgreSQL identifiers never need quoting and are never
/// spelled out property by property.
///
/// Why this is not the EFCore.NamingConventions package (ADR-002): that package
/// is an IConventionSetPlugin, and EF Core builds the __EFMigrationsHistory
/// model from the same convention set. The plugin therefore renames that
/// table's MigrationId/ProductVersion columns as well, which silently works on
/// a fresh database and breaks every upgrade of an existing one. This pass runs
/// inside OnModelCreating, which the history repository never consults, so the
/// Phase 0 migrations history keeps working untouched.
/// </summary>
internal static class SnakeCaseNaming
{
    internal static void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Tables and columns first: key, foreign key and index names are derived
        // from them, so they must already be rewritten when pass two reads them.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();

            if (tableName is not null)
            {
                entity.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
            }
        }

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName()));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName()));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()));
            }
        }
    }

    /// <summary>
    /// PascalCase to snake_case. An underscore is inserted before an upper-case
    /// letter that starts a word -- the previous character is not upper-case, or
    /// the next one is lower-case (so HTTPStatus becomes http_status). Existing
    /// underscores are never doubled.
    /// </summary>
    internal static string? ToSnakeCase(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];

            if (!char.IsUpper(current))
            {
                builder.Append(current);
                continue;
            }

            var previous = i == 0 ? '\0' : name[i - 1];
            var next = i + 1 < name.Length ? name[i + 1] : '\0';

            var startsWord = i > 0
                && previous != '_'
                && (!char.IsUpper(previous) || char.IsLower(next));

            if (startsWord)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}
