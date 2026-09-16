using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace CommerceOps.IntegrationTests;

[Collection(IdentityCollection.Name)]
public sealed class AuthenticationTests(IdentityFixture fixture)
{
    private const string GoodPassword = "correct-horse-battery";

    [Fact]
    public async Task Login_with_correct_credentials_starts_a_session()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = IdentityFixture.UniqueEmail("signin");
        await fixture.CreateUserAsync(email, GoodPassword, roles: "Viewer");

        using var session = await ApiSession.SignedInAsync(fixture, email, GoodPassword, cancellationToken);

        var response = await session.GetAsync("/api/auth/me", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await response.Content.ReadFromJsonAsync<CurrentUser>(ApiSession.Json, cancellationToken);
        Assert.Equal(email, me!.Email);
        Assert.Equal(["Viewer"], me.Roles);
        Assert.Equal(["ProductGroup.View"], me.Permissions);
        Assert.False(me.MustChangePassword);
    }

    /// <summary>
    /// Unknown account, wrong password, locked out and deactivated must be
    /// impossible to tell apart from outside. The reason lives in the log only.
    /// </summary>
    [Fact]
    public async Task The_four_sign_in_failures_return_one_identical_answer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var wrongPasswordEmail = IdentityFixture.UniqueEmail("wrongpw");
        await fixture.CreateUserAsync(wrongPasswordEmail, GoodPassword);

        var lockedEmail = IdentityFixture.UniqueEmail("locked");
        await fixture.CreateUserAsync(lockedEmail, GoodPassword);

        var inactiveEmail = IdentityFixture.UniqueEmail("inactive");
        await fixture.CreateUserAsync(inactiveEmail, GoodPassword, isActive: false);

        using var session = await ApiSession.AnonymousAsync(fixture, cancellationToken);

        // Drive the lockout account past the limit first.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await session.SignInAsync(lockedEmail, "definitely-not-the-password", cancellationToken);
        }

        var answers = new List<(string Scenario, HttpStatusCode Status, string Body)>();

        foreach (var (scenario, email, password) in new[]
                 {
                     ("unknown account", IdentityFixture.UniqueEmail("nobody"), GoodPassword),
                     ("wrong password", wrongPasswordEmail, "definitely-not-the-password"),
                     ("locked out", lockedEmail, GoodPassword),
                     ("deactivated", inactiveEmail, GoodPassword),
                 })
        {
            var response = await session.SignInAsync(email, password, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            answers.Add((scenario, response.StatusCode, WithoutTraceId(body)));
            Assert.Null(response.Headers.Location);
        }

        var expected = answers[0];
        Assert.Equal(HttpStatusCode.Unauthorized, expected.Status);
        Assert.Contains("invalid_credentials", expected.Body, StringComparison.Ordinal);

        foreach (var answer in answers)
        {
            Assert.Equal(expected.Status, answer.Status);
            Assert.Equal(expected.Body, answer.Body);
        }
    }

    /// <summary>
    /// The trace identifier is minted per request and says nothing about the
    /// account, so it is the one field allowed to differ between the answers.
    /// </summary>
    private static string WithoutTraceId(string body)
    {
        var document = JsonNode.Parse(body)!.AsObject();
        document.Remove("traceId");

        return document.ToJsonString();
    }

    [Fact]
    public async Task Anonymous_request_to_a_protected_endpoint_is_401_and_not_a_redirect()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = fixture.CreateClient();

        var response = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("unauthorized", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sign_out_ends_this_session()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = IdentityFixture.UniqueEmail("signout");
        await fixture.CreateUserAsync(email, GoodPassword);

        using var session = await ApiSession.SignedInAsync(fixture, email, GoodPassword, cancellationToken);

        var signOut = await session.SignOutAsync(cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);

        var afterSignOut = await session.GetAsync("/api/auth/me", cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterSignOut.StatusCode);
    }

    [Fact]
    public async Task Lockout_rejects_even_the_correct_password()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = IdentityFixture.UniqueEmail("lockout");
        await fixture.CreateUserAsync(email, GoodPassword);

        using var session = await ApiSession.AnonymousAsync(fixture, cancellationToken);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failure = await session.SignInAsync(email, "definitely-not-the-password", cancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        var withCorrectPassword = await session.SignInAsync(email, GoodPassword, cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, withCorrectPassword.StatusCode);
    }

    [Fact]
    public async Task Api_responses_are_never_cached()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = IdentityFixture.UniqueEmail("nostore");
        await fixture.CreateUserAsync(email, GoodPassword);

        using var session = await ApiSession.SignedInAsync(fixture, email, GoodPassword, cancellationToken);

        var response = await session.GetAsync("/api/auth/me", cancellationToken);

        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    private sealed record CurrentUser(
        Guid Id,
        string Email,
        string DisplayName,
        bool MustChangePassword,
        string[] Roles,
        string[] Permissions);
}
