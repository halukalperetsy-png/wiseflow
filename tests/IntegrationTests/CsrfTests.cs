using System.Net;

namespace CommerceOps.IntegrationTests;

/// <summary>
/// The session is a cookie, so the browser sends it on cross-site requests as
/// well. These assert that the request token is actually required rather than
/// merely issued.
/// </summary>
[Collection(IdentityCollection.Name)]
public sealed class CsrfTests(IdentityFixture fixture)
{
    private const string Password = "correct-horse-battery";

    [Fact]
    public async Task A_state_changing_request_without_a_token_is_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var email = IdentityFixture.UniqueEmail("csrf");
        await fixture.CreateUserAsync(email, Password, roles: "Admin");

        using var session = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        var response = await session.PostWithoutCsrfAsync(
            "/api/product-groups",
            new { code = IdentityFixture.UniqueCode("CSRF"), name = "Token yok" },
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "csrf_failed",
            await response.Content.ReadAsStringAsync(cancellationToken),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_state_changing_request_with_a_forged_token_is_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var email = IdentityFixture.UniqueEmail("csrfforged");
        await fixture.CreateUserAsync(email, Password, roles: "Admin");

        using var session = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        var response = await session.PostWithCsrfTokenAsync(
            "/api/product-groups",
            new { code = IdentityFixture.UniqueCode("CSRF"), name = "Sahte token" },
            "CfDJ8NotARealTokenAtAll",
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "csrf_failed",
            await response.Content.ReadAsStringAsync(cancellationToken),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Signing in is itself a state change, and forced sign-in is a real attack,
    /// so the login endpoint is inside the protection too.
    /// </summary>
    [Fact]
    public async Task Signing_in_without_a_token_is_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var email = IdentityFixture.UniqueEmail("csrflogin");
        await fixture.CreateUserAsync(email, Password, roles: "Viewer");

        using var session = await ApiSession.AnonymousAsync(fixture, cancellationToken);

        var response = await session.PostWithoutCsrfAsync(
            "/api/auth/login", new { email, password = Password }, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "csrf_failed",
            await response.Content.ReadAsStringAsync(cancellationToken),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_request_with_a_valid_token_goes_through()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var email = IdentityFixture.UniqueEmail("csrfok");
        await fixture.CreateUserAsync(email, Password, roles: "Admin");

        using var session = await ApiSession.SignedInAsync(fixture, email, Password, cancellationToken);

        var response = await session.PostAsync(
            "/api/product-groups",
            new { code = IdentityFixture.UniqueCode("OK"), name = "Token var" },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
