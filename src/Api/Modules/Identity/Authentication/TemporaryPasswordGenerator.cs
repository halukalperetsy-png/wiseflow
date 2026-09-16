using System.Security.Cryptography;

namespace CommerceOps.Api.Modules.Identity.Authentication;

/// <summary>
/// Produces the one-time password an administrator hands to a new user.
///
/// The value is returned in the creating response body once and never again: it
/// is not stored (only its hash is), not logged, not placed in a URL and not
/// readable through any GET.
/// </summary>
internal static class TemporaryPasswordGenerator
{
    internal const int Length = 16;

    /// <summary>
    /// Deliberately excludes characters that are easy to confuse when a password
    /// is read out or copied by hand: O, 0, I, l, 1.
    /// </summary>
    internal const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    internal static string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);
}
