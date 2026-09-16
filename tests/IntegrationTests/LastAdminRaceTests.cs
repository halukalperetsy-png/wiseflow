using System.Net;
using CommerceOps.Api.Infrastructure.Persistence;
using CommerceOps.Api.Modules.Identity.Users;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CommerceOps.IntegrationTests;

/// <summary>
/// The failure path of the last-administrator guard, which only a race can
/// reach. It counts every administrator in the database, so it takes a container
/// of its own -- a shared one would have other tests' administrators in it and
/// the guard would never fire.
/// </summary>
public sealed class LastAdminRaceTests(IdentityFixture fixture) : IClassFixture<IdentityFixture>
{
    private const string Password = "correct-horse-battery";

    /// <summary>
    /// Two administrators demote each other at the same moment. Without the
    /// advisory lock both read "one administrator survives", both commit, and the
    /// system is left with none.
    ///
    /// The lock is held from outside for the duration, which parks both requests
    /// inside the guard after they have already passed authorization -- that is
    /// what makes the overlap deterministic rather than a matter of timing.
    /// </summary>
    [Fact]
    public async Task Two_administrators_demoting_each_other_at_once_cannot_empty_the_system()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var firstEmail = IdentityFixture.UniqueEmail("admin-a");
        var secondEmail = IdentityFixture.UniqueEmail("admin-b");
        var firstId = await fixture.CreateUserAsync(firstEmail, Password, roles: "Admin");
        var secondId = await fixture.CreateUserAsync(secondEmail, Password, roles: "Admin");

        Assert.Equal(2, await CountActiveAdministratorsAsync(cancellationToken));

        using var first = await ApiSession.SignedInAsync(fixture, firstEmail, Password, cancellationToken);
        using var second = await ApiSession.SignedInAsync(fixture, secondEmail, Password, cancellationToken);

        await using var blocker = new NpgsqlConnection(fixture.ConnectionString);
        await blocker.OpenAsync(cancellationToken);
        await using var holding = await blocker.BeginTransactionAsync(cancellationToken);

        await using (var takeLock = new NpgsqlCommand(
            $"SELECT pg_advisory_xact_lock({LastAdminGuard.AdvisoryLockKey})", blocker, holding))
        {
            await takeLock.ExecuteNonQueryAsync(cancellationToken);
        }

        var demoteSecond = first.PutAsync(
            $"/api/admin/users/{secondId}/roles", new { roles = new[] { "Viewer" } }, cancellationToken);
        var demoteFirst = second.PutAsync(
            $"/api/admin/users/{firstId}/roles", new { roles = new[] { "Viewer" } }, cancellationToken);

        // Both are now parked on the advisory lock, past authorization.
        await WaitUntilBothAreInFlightAsync(demoteSecond, demoteFirst, cancellationToken);
        await holding.RollbackAsync(cancellationToken);

        var responses = await Task.WhenAll(demoteSecond, demoteFirst);

        var succeeded = responses.Count(response => response.StatusCode == HttpStatusCode.NoContent);
        var refused = responses.Count(response => response.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, succeeded);
        Assert.Equal(1, refused);

        var refusal = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Contains(
            "last_admin_protected",
            await refusal.Content.ReadAsStringAsync(cancellationToken),
            StringComparison.Ordinal);

        Assert.Equal(1, await CountActiveAdministratorsAsync(cancellationToken));

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    private Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken) =>
        fixture.WithScopeAsync(services => LastAdminGuard.CountActiveAdministratorsAsync(
            services.GetRequiredService<CommerceOpsDbContext>(), cancellationToken));

    /// <summary>
    /// Neither request can complete while the lock is held, so "still running"
    /// after a short settle is the signal that both have reached the guard.
    /// </summary>
    private static async Task WaitUntilBothAreInFlightAsync(
        Task<HttpResponseMessage> first,
        Task<HttpResponseMessage> second,
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);

        Assert.False(first.IsCompleted, "The first request finished while the advisory lock was held.");
        Assert.False(second.IsCompleted, "The second request finished while the advisory lock was held.");
    }
}
