using System.Net;
using System.Net.Http.Json;

namespace CommerceOps.IntegrationTests;

/// <summary>
/// What happens to a session that is already open when something about the user
/// changes. The security stamp validator revalidates on every request, so the
/// answers here are all "on the next request".
/// </summary>
[Collection(IdentityCollection.Name)]
public sealed class SessionLifecycleTests(IdentityFixture fixture)
{
    private const string Password = "correct-horse-battery";

    [Fact]
    public async Task Deactivating_a_user_ends_their_open_session_on_the_next_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var email = IdentityFixture.UniqueEmail("victim");
        var userId = await fixture.CreateUserAsync(email, Password, roles: "Viewer");

        using var user = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/auth/me", cancellationToken)).StatusCode);

        using var admin = await SignInAsNewAdminAsync(cancellationToken);
        var deactivated = await admin.PatchAsync(
            $"/api/admin/users/{userId}", new { isActive = false }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        var afterwards = await user.GetAsync("/api/auth/me", cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    /// <summary>
    /// A permission change must reach an open session without forcing the user
    /// to sign in again -- the principal is rebuilt from the database each time.
    /// </summary>
    [Fact]
    public async Task A_product_group_assignment_reaches_an_open_session_without_a_new_sign_in()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var group = await fixture.CreateProductGroupAsync(IdentityFixture.UniqueCode("LATE"), "Sonradan Verilen");
        var email = IdentityFixture.UniqueEmail("grows");
        var userId = await fixture.CreateUserAsync(email, Password, roles: "Viewer");

        using var user = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        var before = await ReadGroupIdsAsync(user, cancellationToken);
        Assert.DoesNotContain(group, before);

        using var admin = await SignInAsNewAdminAsync(cancellationToken);
        var assigned = await admin.PutAsync(
            $"/api/admin/users/{userId}/product-groups",
            new { productGroupIds = new[] { group } },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);

        // Same session, same cookie, no new sign-in.
        var after = await ReadGroupIdsAsync(user, cancellationToken);
        Assert.Contains(group, after);
    }

    [Fact]
    public async Task A_role_change_reaches_an_open_session_without_a_new_sign_in()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var email = IdentityFixture.UniqueEmail("promoted");
        var userId = await fixture.CreateUserAsync(email, Password, roles: "Viewer");

        using var user = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await user.GetAsync("/api/admin/users", cancellationToken)).StatusCode);

        using var admin = await SignInAsNewAdminAsync(cancellationToken);
        var promoted = await admin.PutAsync(
            $"/api/admin/users/{userId}/roles",
            new { roles = new[] { "Admin" } },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, promoted.StatusCode);

        var afterwards = await user.GetAsync("/api/admin/users", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, afterwards.StatusCode);
    }

    /// <summary>
    /// Changing a password ends the user's other sessions but keeps the one that
    /// performed the change.
    /// </summary>
    [Fact]
    public async Task Changing_a_password_ends_the_other_sessions_and_keeps_this_one()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var email = IdentityFixture.UniqueEmail("twodevices");
        await fixture.CreateUserAsync(email, Password, roles: "Viewer");

        using var laptop = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);
        using var phone = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        var changed = await laptop.PostAsync(
            "/api/auth/change-password",
            new { currentPassword = Password, newPassword = "a-completely-new-passphrase" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync("/api/auth/me", cancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await phone.GetAsync("/api/auth/me", cancellationToken)).StatusCode);
    }

    /// <summary>
    /// Signing out closes this session only. Revoking every session of a user is
    /// a separate, deliberate act -- deactivation or an administrator reset.
    /// </summary>
    [Fact]
    public async Task Signing_out_on_one_device_leaves_the_other_device_signed_in()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var email = IdentityFixture.UniqueEmail("onedevice");
        await fixture.CreateUserAsync(email, Password, roles: "Viewer");

        using var laptop = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);
        using var phone = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, (await laptop.SignOutAsync(cancellationToken)).StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await laptop.GetAsync("/api/auth/me", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/auth/me", cancellationToken)).StatusCode);
    }

    [Fact]
    public async Task An_administrator_password_reset_ends_every_session_that_user_has()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var email = IdentityFixture.UniqueEmail("reset");
        var userId = await fixture.CreateUserAsync(email, Password, roles: "Viewer");

        using var laptop = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);
        using var phone = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        using var admin = await SignInAsNewAdminAsync(cancellationToken);
        var reset = await admin.PostAsync(
            $"/api/admin/users/{userId}/reset-password", body: null, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var issued = await reset.Content.ReadFromJsonAsync<IssuedPassword>(ApiSession.Json, cancellationToken);
        Assert.NotNull(issued);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await laptop.GetAsync("/api/auth/me", cancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await phone.GetAsync("/api/auth/me", cancellationToken)).StatusCode);

        // The old password is gone and the issued one lands on the change flow.
        using var stale = await ApiSession.AnonymousAsync(fixture, cancellationToken);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await stale.SignInAsync(email, Password, cancellationToken)).StatusCode);

        using var fresh = await ApiSession.SignedInAsync(
            fixture, email, issued.TemporaryPassword, cancellationToken);
        var blocked = await fresh.GetAsync("/api/product-groups", cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
    }

    private async Task<ApiSession> SignInAsNewAdminAsync(CancellationToken cancellationToken)
    {
        var email = IdentityFixture.UniqueEmail("admin");
        await fixture.CreateUserAsync(email, Password, roles: "Admin");

        return await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);
    }

    private static async Task<IReadOnlyList<Guid>> ReadGroupIdsAsync(
        ApiSession session,
        CancellationToken cancellationToken)
    {
        var response = await session.GetAsync("/api/product-groups", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var groups = await response.Content.ReadFromJsonAsync<List<GroupPayload>>(
            ApiSession.Json, cancellationToken);

        return [.. groups!.Select(group => group.Id)];
    }

    private sealed record GroupPayload(Guid Id, string Code, string Name);

    private sealed record IssuedPassword(Guid Id, string TemporaryPassword);
}
