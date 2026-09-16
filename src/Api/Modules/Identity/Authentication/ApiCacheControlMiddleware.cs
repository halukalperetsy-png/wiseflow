using Microsoft.Net.Http.Headers;

namespace CommerceOps.Api.Modules.Identity.Authentication;

/// <summary>
/// Every /api response is per-user and some carry a one-time password, so none
/// of them may be written to a cache. Set on response start so an endpoint
/// cannot lose it by writing headers later.
/// </summary>
internal sealed class ApiCacheControlMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Path.StartsWithSegments(
                PasswordChangeRequiredMiddleware.ApiPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.OnStarting(static state =>
            {
                var response = (HttpResponse)state;
                response.Headers[HeaderNames.CacheControl] = "no-store";
                return Task.CompletedTask;
            }, context.Response);
        }

        return next(context);
    }
}
