using CommerceOps.Api.Infrastructure.ErrorHandling;
using Microsoft.AspNetCore.Antiforgery;

namespace CommerceOps.Api.Modules.Identity.Authentication;

/// <summary>
/// Validates the CSRF token on every state-changing endpoint.
///
/// The session lives in a cookie, so the browser attaches it to cross-site
/// requests too; SameSite=Lax narrows that but does not close it for top-level
/// POSTs. The request token is read from the X-CSRF-TOKEN header, which a
/// cross-site form cannot set.
/// </summary>
internal sealed class AntiforgeryEndpointFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return ApiProblems.Problem(
                StatusCodes.Status400BadRequest,
                ProblemCodes.CsrfFailed,
                "Oturum doğrulaması başarısız. Sayfayı yenileyip tekrar deneyin.");
        }

        return await next(context);
    }
}

internal static class AntiforgeryEndpointFilterExtensions
{
    /// <summary>Applies CSRF validation. Every POST, PUT, PATCH and DELETE needs it.</summary>
    internal static RouteHandlerBuilder ValidateAntiforgery(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter<AntiforgeryEndpointFilter>();
    }
}
