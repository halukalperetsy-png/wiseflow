using CommerceOps.Api.Infrastructure.ErrorHandling;
using CommerceOps.Api.Modules.Identity.Authorization;

namespace CommerceOps.Api.Modules.Identity.Authentication;

/// <summary>
/// Holds a user who is still on a temporary password to the change-password
/// flow.
///
/// Two conditions must both hold before anything is blocked, which is what
/// keeps the gate from reaching further than intended:
///
/// 1. the request path is under /api -- so GET /health and every static file
///    the SPA serves are structurally outside the gate, and
/// 2. the matched endpoint carries no AllowPendingPasswordChange metadata --
///    so the csrf, login, logout, me and change-password routes stay open.
/// </summary>
internal sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    internal const string ApiPathPrefix = "/api";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Request.Path.StartsWithSegments(ApiPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return next(context);
        }

        if (context.User.Identity?.IsAuthenticated != true || !context.User.MustChangePassword())
        {
            return next(context);
        }

        var endpoint = context.GetEndpoint();

        if (endpoint?.Metadata.GetMetadata<AllowPendingPasswordChangeAttribute>() is not null)
        {
            return next(context);
        }

        return ApiProblems.WriteAsync(
            context.Response,
            StatusCodes.Status403Forbidden,
            ProblemCodes.PasswordChangeRequired,
            "Devam etmeden önce parolanızı değiştirmeniz gerekiyor.");
    }
}
