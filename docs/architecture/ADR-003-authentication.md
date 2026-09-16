# ADR-003: Authentication, sessions and authorization

Status: accepted, Phase 1.

## Context

Phase 1 adds the first thing this application has to get right before anything
else: who may sign in, what they may do, and which product groups' data they may
see. Phase 2 hangs the catalog off those boundaries, so they have to be correct
and tested now rather than revisited later.

Two things must not be invented here: password hashing and the session token.

## Decision

**ASP.NET Core Identity, with a cookie session.**

| Concern | Component |
|---|---|
| Password hashing | Identity's `PasswordHasher<User>` (PBKDF2-HMAC-SHA512, versioned format, rehash detection) |
| Session | `CookieAuthenticationHandler` on `IdentityConstants.ApplicationScheme`, encrypted and signed by Data Protection |
| Lockout | `IdentityOptions.Lockout` + `PasswordSignInAsync(lockoutOnFailure: true)` |
| Email normalisation | Identity's `UpperInvariantLookupNormalizer` |
| Session revocation | `SecurityStampValidator` with `ValidationInterval = TimeSpan.Zero` |
| CSRF | `IAntiforgery`, request token in the `X-CSRF-TOKEN` header |
| Login throttling | Built-in rate limiting middleware, partitioned by client address |

A cookie rather than a bearer token because the SPA is served from the same
origin: there is no token to store, refresh, or leak to script. `HttpOnly` takes
the session out of JavaScript's reach entirely.

### Where it lives

`Modules/Identity/`, created by this phase as `module-conventions.md` says. One
deliberate exception to module isolation: `CommerceOpsDbContext` derives from
`IdentityDbContext<User, Role, Guid>`, so `Infrastructure/Persistence` has a
generic-argument dependency on the Identity module. It is the price of using
Identity's EF stores unchanged, and it is the only such dependency — no other
module gets a `DbSet` on the shared context.

### Session revocation, stated exactly

`ValidationInterval = TimeSpan.Zero` revalidates the principal against the
database on every request. That costs one indexed lookup per authenticated
request, which an admin panel can afford, and buys a policy with no "eventually"
in it:

| Change | Security stamp | Effect on open sessions |
|---|---|---|
| Role added or removed | unchanged | next request, with the new permissions, no sign-out |
| Product group assignment | unchanged | next request (the scope is a query filter anyway) |
| Sign out | **unchanged** | this session's cookie is cleared; the user's other devices are untouched |
| Deactivation | **rotated** | every session of that user, 401 on its next request |
| Administrator password reset | **rotated** | every session of that user, 401 on its next request |
| User changes own password | **rotated** (by Identity) | every *other* session 401s; this one is kept by `RefreshSignInAsync` |

Signing out is not a global revocation. The cookie is cleared in the browser, but
the value stays cryptographically valid until `ExpireTimeSpan` (8 hours), which is
how cookie authentication works. `HttpOnly` + `Secure` + `SameSite=Lax` narrow the
window; deactivation and password reset exist for when it has to be closed now.
This is recorded as a known risk rather than hidden.

Deactivation is enforced twice, in `CommerceOpsSignInManager`: `CanSignInAsync`
refuses a new sign-in, and `ValidateSecurityStampAsync` drops an open session. So
flipping `is_active` directly in SQL still ends the session, without relying on
the stamp having been rotated too.

### Failed sign-in answers one way

Unknown account, wrong password, locked out and deactivated all return the same
401 with `code: "invalid_credentials"` and the same title — byte for byte, save
the per-request trace id. A decoy hash verification runs for an unknown address
so the response time does not answer what the message refuses to. The reason is
written to the server log only, and the submitted email is never logged: a user
who types their password into the email field must not have it end up in a log
file.

The usability this costs is bought back with a permanent line on the sign-in
form — "Çok sayıda hatalı denemeden sonra hesabınız geçici olarak kilitlenir." —
which is true always and signals nothing about any particular attempt.

### Permissions are code, not rows

`identity.permissions` and `identity.role_permissions` are not created. Phase 1
has no screen that could edit them, a permission name has to match its policy
name exactly, and compile-time constants make that impossible to misspell.
Putting them in data now would be the first half of a permission engine with no
consumer. Roles *are* rows — Identity requires them and `user_roles` refers to
them — and the role-to-permission map lives in `RolePermissions.cs`.

Two roles are seeded: `Admin` and `Viewer`. The design document names four more,
but under the Phase 1 permission set all of them would behave exactly like
Viewer; they arrive with the `Product.*` permissions in Phase 2 rather than as
indistinguishable options in a role picker.

### Authorization is two layers

1. A permission policy on the endpoint (`RequireAuthorization(Permissions.X)`).
2. A data filter in the query (`ProductGroupScope`).

A permission alone is never enough. The listing, the single record and the
change endpoint all go through the same `ProductGroupScope.Visible`, so they
cannot drift apart. A record outside the caller's scope answers 404, not 403: a
user who may not see a group should not learn that it exists.

## Consequences

**Gained**

- No hand-written cryptography, token format, or lockout counter.
- Session revocation with no staleness window, and a policy that can be stated
  in one table and tested.
- Turkish locale safety: Identity's invariant normaliser, not `ToUpper()`.
- Zero new backend packages beyond `Microsoft.AspNetCore.Identity.EntityFrameworkCore`.

**Accepted costs**

- Four tables Phase 1 never writes or reads — `user_claims`, `user_logins`,
  `user_tokens`, `role_claims` — plus columns for MFA, phone and email
  confirmation. They are part of Identity's model and cannot be pruned from
  `IdentityDbContext`. They serve features that are explicitly out of scope.
- One database round trip per authenticated request, and a `Set-Cookie` on each
  response. No cache in Phase 1; revisit with a measurement, not a guess.
- `Infrastructure/Persistence` depends on `Modules/Identity` (above).
- Signing out does not revoke other sessions (above).
