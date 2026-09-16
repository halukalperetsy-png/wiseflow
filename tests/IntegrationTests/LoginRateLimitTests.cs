using System.Net;

namespace CommerceOps.IntegrationTests;

/// <summary>
/// A host with the login limit turned down far enough to observe. The shared
/// fixture turns it up instead of off, so the control is exercised here rather
/// than disabled everywhere.
/// </summary>
public sealed class ThrottledLoginFixture : IdentityFixture
{
    internal const int PermitLimit = 3;

    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration =>
    [
        new("Security:LoginRateLimit:PermitLimit", PermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        new("Security:LoginRateLimit:WindowSeconds", "60"),
        new("Logging:LogLevel:Default", "Warning"),
        new("Logging:LogLevel:Microsoft", "Warning"),
    ];
}

/// <summary>
/// Per-account lockout stops guessing at one account. This stops one attempt
/// being sprayed across many accounts, which lockout cannot see.
/// </summary>
public sealed class LoginRateLimitTests(ThrottledLoginFixture fixture) : IClassFixture<ThrottledLoginFixture>
{
    [Fact]
    public async Task Sign_in_attempts_are_throttled_per_client_beyond_the_permitted_number()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var session = await ApiSession.AnonymousAsync(fixture, cancellationToken);

        // Each attempt names a different account, so account lockout never fires:
        // whatever stops this is the rate limiter.
        for (var attempt = 0; attempt < ThrottledLoginFixture.PermitLimit; attempt++)
        {
            var allowed = await session.SignInAsync(
                IdentityFixture.UniqueEmail("spray"), "whatever-it-does-not-matter", cancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var throttled = await session.SignInAsync(
            IdentityFixture.UniqueEmail("spray"), "whatever-it-does-not-matter", cancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.Contains(
            "too_many_requests",
            await throttled.Content.ReadAsStringAsync(cancellationToken),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Throttling_does_not_reach_the_rest_of_the_api()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var session = await ApiSession.AnonymousAsync(fixture, cancellationToken);

        // The limiter is attached to the login endpoint only.
        for (var attempt = 0; attempt < ThrottledLoginFixture.PermitLimit + 3; attempt++)
        {
            var health = await session.GetAsync("/health", cancellationToken);

            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        }
    }
}
