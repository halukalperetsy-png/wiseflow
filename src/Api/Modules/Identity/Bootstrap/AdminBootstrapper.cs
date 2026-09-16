using CommerceOps.Api.Infrastructure.Persistence;
using CommerceOps.Api.Modules.Identity.Roles;
using CommerceOps.Api.Modules.Identity.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CommerceOps.Api.Modules.Identity.Bootstrap;

/// <summary>
/// Creates the first administrator from local configuration.
///
/// Run as a command, not an endpoint: "dotnet run --project src/Api -- bootstrap-admin".
/// Re-running is safe -- an existing account is left with its password
/// untouched and is only ensured to be active and in the Admin role.
/// </summary>
internal static class AdminBootstrapper
{
    internal const string Verb = "bootstrap-admin";

    internal static bool IsRequested(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return args.Contains(Verb, StringComparer.Ordinal);
    }

    /// <summary>
    /// The command-line configuration provider rejects a bare positional
    /// argument, so the verb is removed before the host sees the arguments.
    /// </summary>
    internal static string[] StripVerb(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return [.. args.Where(argument => !string.Equals(argument, Verb, StringComparison.Ordinal))];
    }

    internal static async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap");
        var configuration = provider.GetRequiredService<IConfiguration>();

        BootstrapAdminSettings settings;

        try
        {
            settings = BootstrapSettingsResolver.Resolve(configuration);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError("{Message}", exception.Message);

            return 1;
        }

        var database = provider.GetRequiredService<CommerceOpsDbContext>().Database;
        var pending = await database.GetPendingMigrationsAsync(cancellationToken);

        if (pending.Any())
        {
            logger.LogError(
                "The database is not up to date. Run: dotnet ef database update --project src/Api");

            return 1;
        }

        var userManager = provider.GetRequiredService<UserManager<User>>();
        var timeProvider = provider.GetRequiredService<TimeProvider>();
        var now = timeProvider.GetUtcNow();

        var existing = await userManager.FindByEmailAsync(settings.Email);

        if (existing is not null)
        {
            return await EnsureAdministratorAsync(userManager, logger, existing, now);
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            UserName = settings.Email,
            Email = settings.Email,
            EmailConfirmed = true,
            DisplayName = settings.DisplayName,
            IsActive = true,

            // The operator chose this password themselves, so there is nothing
            // to rotate on first use.
            MustChangePassword = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var created = await userManager.CreateAsync(user, settings.Password);

        if (!created.Succeeded)
        {
            // Codes only -- never the submitted values.
            logger.LogError(
                "Could not create the first administrator: {Errors}",
                string.Join(", ", created.Errors.Select(error => error.Code)));

            return 1;
        }

        var roleAssigned = await userManager.AddToRoleAsync(user, RoleCodes.Admin);

        if (!roleAssigned.Succeeded)
        {
            logger.LogError(
                "Could not assign the {Role} role: {Errors}",
                RoleCodes.Admin,
                string.Join(", ", roleAssigned.Errors.Select(error => error.Code)));

            return 1;
        }

        logger.LogInformation("First administrator created: {UserId}", user.Id);

        return 0;
    }

    private static async Task<int> EnsureAdministratorAsync(
        UserManager<User> userManager,
        ILogger logger,
        User existing,
        DateTimeOffset now)
    {
        var changed = false;

        if (!existing.IsActive)
        {
            existing.IsActive = true;
            changed = true;
        }

        if (!await userManager.IsInRoleAsync(existing, RoleCodes.Admin))
        {
            var assigned = await userManager.AddToRoleAsync(existing, RoleCodes.Admin);

            if (!assigned.Succeeded)
            {
                logger.LogError(
                    "Could not assign the {Role} role: {Errors}",
                    RoleCodes.Admin,
                    string.Join(", ", assigned.Errors.Select(error => error.Code)));

                return 1;
            }

            changed = true;
        }

        if (changed)
        {
            existing.UpdatedAt = now;
            await userManager.UpdateAsync(existing);
        }

        // The password is deliberately not touched: re-running this command must
        // never silently change a credential somebody is already using.
        logger.LogInformation(
            "Administrator {UserId} already exists. Ensured active and in the {Role} role; password unchanged.",
            existing.Id,
            RoleCodes.Admin);

        return 0;
    }
}
