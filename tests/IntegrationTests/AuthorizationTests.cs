using System.Net;
using System.Net.Http.Json;

namespace CommerceOps.IntegrationTests;

[Collection(IdentityCollection.Name)]
public sealed class AuthorizationTests(IdentityFixture fixture)
{
    private const string Password = "correct-horse-battery";

    [Fact]
    public async Task A_viewer_may_not_reach_an_administration_endpoint()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = IdentityFixture.UniqueEmail("viewer");
        await fixture.CreateUserAsync(email, Password, roles: "Viewer");

        using var session = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        var users = await session.GetAsync("/api/admin/users", cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, users.StatusCode);

        var createGroup = await session.PostAsync(
            "/api/product-groups", new { code = "NOPE", name = "Nope" }, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, createGroup.StatusCode);
    }

    [Fact]
    public async Task An_administrator_may_manage_users()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await SignInAsNewAdminAsync(cancellationToken);

        var response = await admin.GetAsync("/api/admin/users", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_user_may_not_change_their_own_roles_or_product_groups()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = IdentityFixture.UniqueEmail("selfadmin");
        var adminId = await fixture.CreateUserAsync(email, Password, roles: "Admin");

        using var session = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        var roles = await session.PutAsync(
            $"/api/admin/users/{adminId}/roles", new { roles = new[] { "Admin" } }, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, roles.StatusCode);
        Assert.Contains(
            "self_role_change_forbidden",
            await roles.Content.ReadAsStringAsync(cancellationToken),
            StringComparison.Ordinal);

        var groups = await session.PutAsync(
            $"/api/admin/users/{adminId}/product-groups",
            new { productGroupIds = Array.Empty<Guid>() },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, groups.StatusCode);
    }

    [Fact]
    public async Task A_user_may_not_deactivate_their_own_account()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = IdentityFixture.UniqueEmail("selfoff");
        var adminId = await fixture.CreateUserAsync(email, Password, roles: "Admin");

        using var session = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        var response = await session.PatchAsync(
            $"/api/admin/users/{adminId}", new { isActive = false }, cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(
            "self_deactivation_forbidden",
            await response.Content.ReadAsStringAsync(cancellationToken),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The gate has to hold the user to the change-password flow without
    /// breaking the flow itself -- or the very first sign-in is a dead end.
    /// </summary>
    [Fact]
    public async Task The_password_change_gate_blocks_the_api_but_not_the_flow_that_clears_it()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = IdentityFixture.UniqueEmail("pending");
        await fixture.CreateUserAsync(email, Password, mustChangePassword: true, roles: "Viewer");

        using var session = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        // Exempt: the session endpoints and the CSRF token.
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/auth/me", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/auth/csrf", cancellationToken)).StatusCode);

        // Outside the exemption: blocked with a code the SPA can route on.
        var blocked = await session.GetAsync("/api/product-groups", cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Contains(
            "password_change_required",
            await blocked.Content.ReadAsStringAsync(cancellationToken),
            StringComparison.Ordinal);

        var changed = await session.PostAsync(
            "/api/auth/change-password",
            new { currentPassword = Password, newPassword = "a-brand-new-passphrase" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        // The gate is open, and the session that changed the password survived.
        var afterwards = await session.GetAsync("/api/product-groups", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, afterwards.StatusCode);
    }

    [Fact]
    public async Task The_password_change_gate_leaves_the_health_endpoint_alone()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = IdentityFixture.UniqueEmail("pendinghealth");
        await fixture.CreateUserAsync(email, Password, mustChangePassword: true, roles: "Viewer");

        using var session = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        // /health is outside /api, so the gate is structurally unable to reach it.
        var health = await session.GetAsync("/health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task A_generated_password_is_returned_once_and_is_never_readable_again()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await SignInAsNewAdminAsync(cancellationToken);

        var newUserEmail = IdentityFixture.UniqueEmail("created");
        var response = await admin.PostAsync(
            "/api/admin/users",
            new { email = newUserEmail, displayName = "Yeni Kullanıcı", roles = new[] { "Viewer" } },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);

        var created = await response.Content.ReadFromJsonAsync<CreatedUser>(ApiSession.Json, cancellationToken);
        Assert.NotNull(created);
        Assert.Equal(16, created.TemporaryPassword.Length);

        // Readable exactly once: the detail endpoint carries no password field.
        var detail = await admin.GetAsync($"/api/admin/users/{created.Id}", cancellationToken);
        var detailBody = await detail.Content.ReadAsStringAsync(cancellationToken);
        Assert.DoesNotContain(created.TemporaryPassword, detailBody, StringComparison.Ordinal);
        Assert.DoesNotContain("emporaryPassword", detailBody, StringComparison.Ordinal);

        // And it really is the password: the new user can sign in with it.
        using var newUser = await ApiSession.SignedInAsync(
            fixture, newUserEmail, created.TemporaryPassword, cancellationToken);

        var me = await newUser.GetAsync("/api/auth/me", cancellationToken);
        var meBody = await me.Content.ReadFromJsonAsync<MePayload>(ApiSession.Json, cancellationToken);
        Assert.True(meBody!.MustChangePassword);
    }

    [Fact]
    public async Task Role_codes_and_product_group_ids_from_the_client_are_checked_against_the_database()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await SignInAsNewAdminAsync(cancellationToken);

        var invalidRole = await admin.PostAsync(
            "/api/admin/users",
            new
            {
                email = IdentityFixture.UniqueEmail("badrole"),
                displayName = "Sahte Rol",
                roles = new[] { "SuperAdmin" },
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, invalidRole.StatusCode);
        Assert.Contains(
            "roles",
            await invalidRole.Content.ReadAsStringAsync(cancellationToken),
            StringComparison.Ordinal);

        var invalidGroup = await admin.PostAsync(
            "/api/admin/users",
            new
            {
                email = IdentityFixture.UniqueEmail("badgroup"),
                displayName = "Sahte Grup",
                productGroupIds = new[] { Guid.NewGuid() },
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, invalidGroup.StatusCode);
        Assert.Contains(
            "productGroupIds",
            await invalidGroup.Content.ReadAsStringAsync(cancellationToken),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_duplicate_email_is_rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await SignInAsNewAdminAsync(cancellationToken);

        var email = IdentityFixture.UniqueEmail("dup");
        var first = await admin.PostAsync(
            "/api/admin/users", new { email, displayName = "İlk Kayıt" }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await admin.PostAsync(
            "/api/admin/users", new { email, displayName = "İkinci Kayıt" }, cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains(
            "email_already_exists",
            await second.Content.ReadAsStringAsync(cancellationToken),
            StringComparison.Ordinal);
    }

    private async Task<ApiSession> SignInAsNewAdminAsync(CancellationToken cancellationToken)
    {
        var email = IdentityFixture.UniqueEmail("admin");
        await fixture.CreateUserAsync(email, Password, roles: "Admin");

        return await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);
    }

    private sealed record CreatedUser(Guid Id, string TemporaryPassword);

    private sealed record MePayload(Guid Id, string Email, string DisplayName, bool MustChangePassword);
}
