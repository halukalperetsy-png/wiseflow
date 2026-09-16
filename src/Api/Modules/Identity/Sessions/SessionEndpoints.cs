using System.Security.Claims;
using CommerceOps.Api.Infrastructure.ErrorHandling;
using CommerceOps.Api.Modules.Identity.Authentication;
using CommerceOps.Api.Modules.Identity.Authorization;
using CommerceOps.Api.Modules.Identity.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace CommerceOps.Api.Modules.Identity.Sessions;

/// <summary>
/// Sign in, sign out, read the current session, change a password.
///
/// Every route here is exempt from the pending-password-change gate: they are
/// the flow that clears it, so gating them would lock a new user out of the
/// only door they have.
/// </summary>
/// <summary>Logger category for the session slice.</summary>
internal sealed class SessionLog;

internal static class SessionEndpoints
{
    internal static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/auth/csrf", IssueCsrfToken)
            .AllowAnonymous()
            .AllowPendingPasswordChange();

        app.MapPost("/api/auth/login", LoginAsync)
            .AllowAnonymous()
            .ValidateAntiforgery()
            .RequireRateLimiting(IdentityRegistration.LoginRateLimitPolicy)
            .AllowPendingPasswordChange();

        app.MapPost("/api/auth/logout", LogoutAsync)
            .RequireAuthorization()
            .ValidateAntiforgery()
            .AllowPendingPasswordChange();

        app.MapGet("/api/auth/me", CurrentUser)
            .RequireAuthorization()
            .AllowPendingPasswordChange();

        app.MapPost("/api/auth/change-password", ChangePasswordAsync)
            .RequireAuthorization()
            .ValidateAntiforgery()
            .AllowPendingPasswordChange();

        return app;
    }

    private static IResult IssueCsrfToken(HttpContext httpContext, IAntiforgery antiforgery)
    {
        // The token is bound to the caller's identity, so the client asks again
        // after signing in and after signing out.
        var tokens = antiforgery.GetAndStoreTokens(httpContext);

        return TypedResults.Ok(new CsrfTokenResponse(tokens.RequestToken ?? string.Empty));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ILogger<SessionLog> logger)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["email"] = ["E-posta ve parola zorunludur."],
            });
        }

        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            // Equalise the timing so a missing account cannot be told apart from
            // a wrong password by how long the answer takes.
            LoginTimingDecoy.Verify(userManager.PasswordHasher, request.Password);
            logger.LogWarning("Login failed: UnknownAccount");

            return InvalidCredentials();
        }

        var result = await signInManager.PasswordSignInAsync(
            user, request.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            logger.LogInformation("Login succeeded for {UserId}", user.Id);

            return TypedResults.NoContent();
        }

        // Every branch returns the same document; only the log knows which.
        var reason = result.IsLockedOut ? "LockedOut"
            : result.IsNotAllowed ? "Inactive"
            : "WrongPassword";

        logger.LogWarning("Login failed: {Reason} for {UserId}", reason, user.Id);

        return InvalidCredentials();
    }

    private static IResult InvalidCredentials() => ApiProblems.Problem(
        StatusCodes.Status401Unauthorized,
        ProblemCodes.InvalidCredentials,
        "E-posta veya parola hatalı.");

    private static async Task<IResult> LogoutAsync(SignInManager<User> signInManager)
    {
        // Ends this session only. Revoking every session of a user is a separate,
        // deliberate act: deactivation or an administrator password reset.
        await signInManager.SignOutAsync();

        return TypedResults.NoContent();
    }

    private static IResult CurrentUser(HttpContext httpContext)
    {
        var principal = httpContext.User;

        return TypedResults.Ok(new CurrentUserResponse(
            principal.GetRequiredUserId(),
            principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            principal.GetDisplayName(),
            principal.MustChangePassword(),
            principal.GetRoleCodes(),
            principal.GetPermissions()));
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        HttpContext httpContext,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        TimeProvider timeProvider)
    {
        if (request is null
            || string.IsNullOrEmpty(request.CurrentPassword)
            || string.IsNullOrEmpty(request.NewPassword))
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["newPassword"] = ["Mevcut ve yeni parola zorunludur."],
            });
        }

        if (request.NewPassword.Length > PasswordRules.MaximumLength)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["newPassword"] = [$"Parola en fazla {PasswordRules.MaximumLength} karakter olabilir."],
            });
        }

        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            return ApiProblems.Problem(
                StatusCodes.Status401Unauthorized,
                ProblemCodes.Unauthorized,
                "Oturum açmanız gerekiyor.");
        }

        var result = await userManager.ChangePasswordAsync(
            user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            return ApiProblems.Validation(
                IdentityErrorTranslator.ToValidationErrors(result.Errors, "newPassword"));
        }

        user.MustChangePassword = false;
        user.UpdatedAt = timeProvider.GetUtcNow();
        await userManager.UpdateAsync(user);

        // ChangePasswordAsync rotated the security stamp, which ends every other
        // session on its next request. Refresh this one so the user who just
        // changed their password is not signed out by their own action.
        await signInManager.RefreshSignInAsync(user);

        return TypedResults.NoContent();
    }
}
