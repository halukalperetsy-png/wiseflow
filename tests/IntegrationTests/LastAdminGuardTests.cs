using System.Net;
using CommerceOps.Api.Infrastructure.Persistence;
using CommerceOps.Api.Modules.Identity.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceOps.IntegrationTests;

/// <summary>
/// The last-administrator rule is about database-wide state, so this class gets
/// its own container rather than joining the shared identity collection: the
/// count has to mean "every administrator there is".
///
/// With the self-change rules in place there is no single-threaded way to reach
/// the failure -- the only account that could leave the system without an
/// administrator is the caller's own, and changing your own roles or deactivating
/// yourself is refused earlier with a 403. These cases therefore assert that the
/// guard does NOT fire when another administrator remains; the failure path lives
/// in LastAdminRaceTests, which needs a database of its own to count in.
/// </summary>
public sealed class LastAdminGuardTests(IdentityFixture fixture) : IClassFixture<IdentityFixture>
{
    private const string Password = "correct-horse-battery";

    [Fact]
    public async Task Demoting_an_administrator_while_another_remains_is_allowed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var actingEmail = IdentityFixture.UniqueEmail("keeper");
        await fixture.CreateUserAsync(actingEmail, Password, roles: "Admin");

        var spareId = await fixture.CreateUserAsync(
            IdentityFixture.UniqueEmail("spare"), Password, roles: "Admin");

        using var acting = await ApiSession.SignedInAsync(fixture, actingEmail, Password, cancellationToken);

        var response = await acting.PutAsync(
            $"/api/admin/users/{spareId}/roles", new { roles = new[] { "Viewer" } }, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(await CountActiveAdministratorsAsync(cancellationToken) >= 1);
    }

    [Fact]
    public async Task Deactivating_an_administrator_while_another_remains_is_allowed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var actingEmail = IdentityFixture.UniqueEmail("keeper2");
        await fixture.CreateUserAsync(actingEmail, Password, roles: "Admin");

        var spareId = await fixture.CreateUserAsync(
            IdentityFixture.UniqueEmail("spare2"), Password, roles: "Admin");

        using var acting = await ApiSession.SignedInAsync(fixture, actingEmail, Password, cancellationToken);

        var response = await acting.PatchAsync(
            $"/api/admin/users/{spareId}", new { isActive = false }, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await CountActiveAdministratorsAsync(cancellationToken) >= 1);
    }

    private Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken) =>
        fixture.WithScopeAsync(services => LastAdminGuard.CountActiveAdministratorsAsync(
            services.GetRequiredService<CommerceOpsDbContext>(), cancellationToken));
}
