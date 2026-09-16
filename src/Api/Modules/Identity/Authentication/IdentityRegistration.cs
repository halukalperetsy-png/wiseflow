using System.Threading.RateLimiting;
using CommerceOps.Api.Infrastructure.ErrorHandling;
using CommerceOps.Api.Infrastructure.Persistence;
using CommerceOps.Api.Modules.Identity.Roles;
using CommerceOps.Api.Modules.Identity.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CommerceOps.Api.Modules.Identity.Authentication;

/// <summary>
/// Wires ASP.NET Core Identity, the session cookie, CSRF and login throttling.
/// Password hashing and the session token are both framework components: this
/// application designs neither. See ADR-003.
/// </summary>
internal static class IdentityRegistration
{
    internal const string AuthCookieName = "commerceops.auth";
    internal const string CsrfCookieName = "commerceops.csrf";
    internal const string CsrfHeaderName = "X-CSRF-TOKEN";
    internal const string LoginRateLimitPolicy = "login";

    internal const int LockoutMaxFailedAttempts = 5;
    internal static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    internal static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    internal static IServiceCollection AddCommerceOpsIdentity(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);

        services.AddIdentityCore<User>(options =>
        {
            options.User.RequireUniqueEmail = true;

            // Length over composition: NIST-aligned, and the rule a user can
            // satisfy without writing the password on a note.
            options.Password.RequiredLength = PasswordRules.MinimumLength;
            options.Password.RequiredUniqueChars = 1;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;

            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = LockoutMaxFailedAttempts;
            options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;

            // There is no email service in scope, so nothing could confirm.
            options.SignIn.RequireConfirmedAccount = false;
            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedPhoneNumber = false;
        })
        .AddRoles<Role>()
        .AddEntityFrameworkStores<CommerceOpsDbContext>()
        .AddSignInManager<CommerceOpsSignInManager>()
        // After AddRoles: that call replaces the factory this one overrides.
        .AddClaimsPrincipalFactory<PermissionClaimsPrincipalFactory>();

        services.TryAddScoped<ISecurityStampValidator, SecurityStampValidator<User>>();
        services.TryAddScoped<ITwoFactorSecurityStampValidator, TwoFactorSecurityStampValidator<User>>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = AuthCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = SecurePolicyFor(environment);
            options.Cookie.Path = "/";
            options.ExpireTimeSpan = SessionLifetime;
            options.SlidingExpiration = true;

            // Individual delegates only. Replacing options.Events would discard
            // the OnValidatePrincipal handler AddIdentityCookies installed, and
            // with it every session revocation guarantee below.
            options.Events.OnRedirectToLogin = context => ApiProblems.WriteAsync(
                context.Response,
                StatusCodes.Status401Unauthorized,
                ProblemCodes.Unauthorized,
                "Oturum açmanız gerekiyor.");

            options.Events.OnRedirectToAccessDenied = context => ApiProblems.WriteAsync(
                context.Response,
                StatusCodes.Status403Forbidden,
                ProblemCodes.Forbidden,
                "Bu işlem için yetkiniz yok.");
        });

        // Zero: the principal is revalidated against the database on every
        // request. Deactivation and password changes take effect on the next
        // request, and a role change refreshes without forcing a new sign-in.
        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.Zero);

        services.AddAntiforgery(options =>
        {
            options.HeaderName = CsrfHeaderName;
            options.Cookie.Name = CsrfCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = SecurePolicyFor(environment);
        });

        AddLoginRateLimiter(services);

        return services;
    }

    private static CookieSecurePolicy SecurePolicyFor(IWebHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment("Testing")
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

    // Per-account lockout stops guessing at one account; this stops spraying one
    // attempt across many. Built-in middleware, no new service, no store.
    private static void AddLoginRateLimiter(IServiceCollection services)
    {
        services.AddOptions<LoginRateLimitOptions>().BindConfiguration(LoginRateLimitOptions.SectionName);

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(LoginRateLimitPolicy, context =>
            {
                // Resolved per partition, not per request: the factory below runs
                // once for each distinct client address.
                var settings = context.RequestServices
                    .GetRequiredService<IOptions<LoginRateLimitOptions>>().Value;

                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                        QueueLimit = 0,
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
                await ApiProblems.WriteAsync(
                    context.HttpContext.Response,
                    StatusCodes.Status429TooManyRequests,
                    ProblemCodes.TooManyRequests,
                    "Çok fazla deneme yapıldı. Lütfen biraz bekleyip tekrar deneyin.").ConfigureAwait(false);
        });
    }
}
