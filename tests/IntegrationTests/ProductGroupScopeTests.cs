using System.Net;
using System.Net.Http.Json;

namespace CommerceOps.IntegrationTests;

/// <summary>
/// The listing, the single record and the change endpoint must all apply the
/// same scope. A permission check alone is never sufficient.
/// </summary>
[Collection(IdentityCollection.Name)]
public sealed class ProductGroupScopeTests(IdentityFixture fixture)
{
    private const string Password = "correct-horse-battery";

    [Fact]
    public async Task A_user_sees_only_the_groups_assigned_to_them()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var mine = await fixture.CreateProductGroupAsync(IdentityFixture.UniqueCode("MINE"), "Benim Grubum");
        var theirs = await fixture.CreateProductGroupAsync(IdentityFixture.UniqueCode("THEIRS"), "Başka Grup");

        var email = IdentityFixture.UniqueEmail("scoped");
        var userId = await fixture.CreateUserAsync(email, Password, roles: "Viewer");
        await fixture.AssignProductGroupAsync(userId, mine);

        using var session = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        var groups = await ReadGroupsAsync(session, cancellationToken);

        Assert.Contains(groups, group => group.Id == mine);
        Assert.DoesNotContain(groups, group => group.Id == theirs);
    }

    [Fact]
    public async Task An_inactive_group_disappears_from_a_users_list_but_not_from_an_administrators()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var retired = await fixture.CreateProductGroupAsync(
            IdentityFixture.UniqueCode("RETIRED"), "Kapatılmış Grup", isActive: false);

        var email = IdentityFixture.UniqueEmail("retired");
        var userId = await fixture.CreateUserAsync(email, Password, roles: "Viewer");
        await fixture.AssignProductGroupAsync(userId, retired);

        using var session = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);
        var visible = await ReadGroupsAsync(session, cancellationToken);
        Assert.DoesNotContain(visible, group => group.Id == retired);

        using var admin = await SignInAsNewAdminAsync(cancellationToken);
        var everything = await ReadGroupsAsync(admin, cancellationToken);
        Assert.Contains(everything, group => group.Id == retired);
    }

    /// <summary>
    /// Out of scope answers 404, not 403: a caller who may not see a group is
    /// not told that it exists.
    /// </summary>
    [Fact]
    public async Task Reading_a_group_outside_the_users_scope_answers_404()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var theirs = await fixture.CreateProductGroupAsync(IdentityFixture.UniqueCode("HIDDEN"), "Gizli Grup");
        var email = IdentityFixture.UniqueEmail("outside");
        await fixture.CreateUserAsync(email, Password, roles: "Viewer");

        using var session = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        var response = await session.GetAsync($"/api/product-groups/{theirs}", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Changing_a_group_outside_the_users_scope_is_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var theirs = await fixture.CreateProductGroupAsync(IdentityFixture.UniqueCode("LOCKED"), "Kilitli Grup");
        var email = IdentityFixture.UniqueEmail("nochange");
        var userId = await fixture.CreateUserAsync(email, Password, roles: "Viewer");

        // Assigned, so this is about the permission rather than the assignment.
        await fixture.AssignProductGroupAsync(userId, theirs);

        using var session = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        var response = await session.PatchAsync(
            $"/api/product-groups/{theirs}", new { name = "Ele Geçirildi" }, cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_administrator_creates_edits_and_retires_a_group()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await SignInAsNewAdminAsync(cancellationToken);

        var code = IdentityFixture.UniqueCode("ORN");
        var created = await admin.PostAsync(
            "/api/product-groups",
            new { code, name = "Süs Eşyaları", description = "Ornaments" },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var group = await created.Content.ReadFromJsonAsync<GroupPayload>(ApiSession.Json, cancellationToken);
        Assert.True(group!.IsActive);

        var renamed = await admin.PatchAsync(
            $"/api/product-groups/{group.Id}", new { name = "Süs Eşyaları TR" }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        var retired = await admin.PatchAsync(
            $"/api/product-groups/{group.Id}", new { isActive = false }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);

        var after = await retired.Content.ReadFromJsonAsync<GroupPayload>(ApiSession.Json, cancellationToken);
        Assert.False(after!.IsActive);
        Assert.Equal("Süs Eşyaları TR", after.Name);
    }

    [Fact]
    public async Task A_duplicate_code_is_refused_regardless_of_casing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await SignInAsNewAdminAsync(cancellationToken);

        var code = IdentityFixture.UniqueCode("DUP");

        var first = await admin.PostAsync(
            "/api/product-groups", new { code, name = "İlk" }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await admin.PostAsync(
            "/api/product-groups",
            new { code = code.ToLowerInvariant(), name = "İkinci" },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains(
            "product_group_code_already_exists",
            await second.Content.ReadAsStringAsync(cancellationToken),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_invalid_code_is_rejected_with_a_field_message()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await SignInAsNewAdminAsync(cancellationToken);

        var response = await admin.PostAsync(
            "/api/product-groups", new { code = "a b!", name = "Geçersiz" }, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("validation_failed", body, StringComparison.Ordinal);
        Assert.Contains("code", body, StringComparison.Ordinal);
    }

    private async Task<ApiSession> SignInAsNewAdminAsync(CancellationToken cancellationToken)
    {
        var email = IdentityFixture.UniqueEmail("admin");
        await fixture.CreateUserAsync(email, Password, roles: "Admin");

        return await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);
    }

    private static async Task<IReadOnlyList<GroupPayload>> ReadGroupsAsync(
        ApiSession session,
        CancellationToken cancellationToken)
    {
        var response = await session.GetAsync("/api/product-groups", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<List<GroupPayload>>(
            ApiSession.Json, cancellationToken))!;
    }

    private sealed record GroupPayload(Guid Id, string Code, string Name, string? Description, bool IsActive);
}
