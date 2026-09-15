namespace CommerceOps.Api.Infrastructure.Configuration;

/// <summary>
/// Resolves the database connection string. The value never lives in the
/// repository: development reads it from .NET User Secrets, CI and production
/// from the ConnectionStrings__CommerceOpsDb environment variable.
/// </summary>
internal static class ConnectionStringResolver
{
    internal const string Name = "CommerceOpsDb";

    /// <summary>Message shown when the connection string is absent. Names the exact fix.</summary>
    internal static string MissingMessage =>
        $"Connection string '{Name}' is not configured. Set it with: " +
        $"dotnet user-secrets set \"ConnectionStrings:{Name}\" " +
        "\"Host=127.0.0.1;Port=5432;Database=commerceops;Username=commerceops;Password=<your dev password>\" " +
        "--project src/Api";

    internal static string Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(Name);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(MissingMessage);
        }

        return connectionString;
    }
}
