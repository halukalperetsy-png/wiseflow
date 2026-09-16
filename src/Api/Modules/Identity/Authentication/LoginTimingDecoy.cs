using CommerceOps.Api.Modules.Identity.Users;
using Microsoft.AspNetCore.Identity;

namespace CommerceOps.Api.Modules.Identity.Authentication;

/// <summary>
/// A sign-in attempt for an address that has no account must cost the same as
/// one for an address that does, or the response time answers the question the
/// error message refuses to.
///
/// The decoy hash is computed once from a value that is never a real password.
/// </summary>
internal static class LoginTimingDecoy
{
    private static readonly User DecoyUser = new() { DisplayName = "decoy" };

    private static readonly string DecoyHash =
        new PasswordHasher<User>().HashPassword(DecoyUser, Guid.NewGuid().ToString("N"));

    internal static void Verify(IPasswordHasher<User> passwordHasher, string suppliedPassword)
    {
        ArgumentNullException.ThrowIfNull(passwordHasher);

        _ = passwordHasher.VerifyHashedPassword(DecoyUser, DecoyHash, suppliedPassword);
    }
}
