namespace CommerceOps.Api.Infrastructure.ErrorHandling;

/// <summary>
/// Stable, machine-readable error codes carried in the "code" extension of every
/// problem document. They are English identifiers; the frontend maps them to
/// Turkish text and falls back to the problem's title when a code is unknown.
/// </summary>
internal static class ProblemCodes
{
    internal const string Unauthorized = "unauthorized";
    internal const string Forbidden = "forbidden";
    internal const string NotFound = "not_found";
    internal const string ValidationFailed = "validation_failed";
    internal const string CsrfFailed = "csrf_failed";

    /// <summary>
    /// The single answer to every failed sign-in. Unknown account, wrong
    /// password, locked out and deactivated all return this, byte for byte --
    /// the reason is only ever written to the server log.
    /// </summary>
    internal const string InvalidCredentials = "invalid_credentials";

    internal const string PasswordChangeRequired = "password_change_required";
    internal const string EmailAlreadyExists = "email_already_exists";
    internal const string ProductGroupCodeAlreadyExists = "product_group_code_already_exists";
    internal const string SelfRoleChangeForbidden = "self_role_change_forbidden";
    internal const string SelfDeactivationForbidden = "self_deactivation_forbidden";
    internal const string LastAdminProtected = "last_admin_protected";
    internal const string TooManyRequests = "too_many_requests";
}
