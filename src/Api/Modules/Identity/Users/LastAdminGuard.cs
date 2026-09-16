using CommerceOps.Api.Infrastructure.ErrorHandling;
using CommerceOps.Api.Infrastructure.Persistence;
using CommerceOps.Api.Modules.Identity.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CommerceOps.Api.Modules.Identity.Users;

/// <summary>
/// Stops the system from ending up with no administrator at all.
///
/// The check cannot be a plain read-then-write. Two administrators can each
/// demote the other at the same moment: both see one survivor, both commit, and
/// nobody is left. So the work happens inside one transaction that takes a
/// PostgreSQL advisory lock FIRST, applies the change, and only then counts the
/// administrators that remain. The order is the whole point -- counting before
/// the lock reintroduces the race.
///
/// With the self-change rules in place there is no single-threaded path to this
/// failure (the only account that could leave the system without an admin is the
/// caller's own, and that is refused earlier), so the concurrency test is where
/// this guard is really exercised.
/// </summary>
internal static class LastAdminGuard
{
    /// <summary>
    /// One fixed key: every change to the administrator population serialises on
    /// it. Released automatically when the transaction ends.
    /// </summary>
    internal const long AdvisoryLockKey = 911001;

    /// <summary>
    /// Runs <paramref name="change"/> under the guard. Returns null when the
    /// change was committed, or the problem result that should be sent instead.
    /// </summary>
    internal static async Task<IResult?> RunAsync(
        CommerceOpsDbContext context,
        Func<Task<IResult?>> change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(change);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // First. A concurrent request now waits here until this one commits.
        await context.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock({AdvisoryLockKey})", cancellationToken);

        var failure = await change();

        if (failure is not null)
        {
            await transaction.RollbackAsync(cancellationToken);

            return failure;
        }

        // Re-queried after the lock and after the change: this is the number of
        // administrators that would actually survive the commit.
        if (await CountActiveAdministratorsAsync(context, cancellationToken) == 0)
        {
            await transaction.RollbackAsync(cancellationToken);

            return ApiProblems.Conflict(
                ProblemCodes.LastAdminProtected,
                "Sistemde en az bir aktif yönetici kalmalıdır.");
        }

        await transaction.CommitAsync(cancellationToken);

        return null;
    }

    internal static Task<int> CountActiveAdministratorsAsync(
        CommerceOpsDbContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var adminRoleId = RoleConfiguration.AdminRoleId;

        return context.Set<User>()
            .Where(user => user.IsActive && context.Set<IdentityUserRole<Guid>>()
                .Any(assignment => assignment.UserId == user.Id && assignment.RoleId == adminRoleId))
            .CountAsync(cancellationToken);
    }
}
