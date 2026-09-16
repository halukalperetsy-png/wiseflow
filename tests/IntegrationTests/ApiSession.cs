using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CommerceOps.Api.Modules.Identity.Authentication;

namespace CommerceOps.IntegrationTests;

/// <summary>
/// One browser-shaped conversation with the API: it keeps cookies, fetches the
/// CSRF request token and sends it on every unsafe request, exactly as the SPA
/// does. Tests that want to probe the CSRF control itself use the Raw methods.
/// </summary>
public sealed class ApiSession(HttpClient client) : IDisposable
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string? _csrfToken;

    public HttpClient Client => client;

    public static async Task<ApiSession> AnonymousAsync(IdentityFixture fixture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        var session = new ApiSession(fixture.CreateClient());
        await session.RefreshCsrfTokenAsync(cancellationToken);

        return session;
    }

    public static async Task<ApiSession> SignedInAsync(
        IdentityFixture fixture,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var session = await AnonymousAsync(fixture, cancellationToken);
        var response = await session.SignInAsync(email, password, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        return session;
    }

    /// <summary>
    /// The antiforgery token is bound to the caller's identity, so it is fetched
    /// again after every sign-in and sign-out -- the same thing the SPA does.
    /// </summary>
    public async Task<string> RefreshCsrfTokenAsync(CancellationToken cancellationToken)
    {
        var response = await client.GetAsync(new Uri("/api/auth/csrf", UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CsrfPayload>(Json, cancellationToken);
        _csrfToken = payload!.Token;

        return _csrfToken;
    }

    public async Task<HttpResponseMessage> SignInAsync(string email, string password, CancellationToken cancellationToken)
    {
        var response = await PostAsync("/api/auth/login", new { email, password }, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            await RefreshCsrfTokenAsync(cancellationToken);
        }

        return response;
    }

    public async Task<HttpResponseMessage> SignOutAsync(CancellationToken cancellationToken)
    {
        var response = await PostAsync("/api/auth/logout", body: null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            await RefreshCsrfTokenAsync(cancellationToken);
        }

        return response;
    }

    public Task<HttpResponseMessage> GetAsync(string path, CancellationToken cancellationToken) =>
        client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);

    public Task<HttpResponseMessage> PostAsync(string path, object? body, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, path, body, withCsrf: true, cancellationToken);

    public Task<HttpResponseMessage> PutAsync(string path, object? body, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, path, body, withCsrf: true, cancellationToken);

    public Task<HttpResponseMessage> PatchAsync(string path, object? body, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Patch, path, body, withCsrf: true, cancellationToken);

    /// <summary>Sends without the CSRF header. For the CSRF tests only.</summary>
    public Task<HttpResponseMessage> PostWithoutCsrfAsync(string path, object? body, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, path, body, withCsrf: false, cancellationToken);

    public Task<HttpResponseMessage> PostWithCsrfTokenAsync(
        string path,
        object? body,
        string token,
        CancellationToken cancellationToken)
    {
        var request = BuildRequest(HttpMethod.Post, path, body);
        request.Headers.Add(IdentityRegistration.CsrfHeaderName, token);

        return client.SendAsync(request, cancellationToken);
    }

    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        bool withCsrf,
        CancellationToken cancellationToken)
    {
        var request = BuildRequest(method, path, body);

        if (withCsrf && _csrfToken is not null)
        {
            request.Headers.Add(IdentityRegistration.CsrfHeaderName, _csrfToken);
        }

        return client.SendAsync(request, cancellationToken);
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        }

        return request;
    }

    public void Dispose() => client.Dispose();

    private sealed record CsrfPayload(string Token);
}
