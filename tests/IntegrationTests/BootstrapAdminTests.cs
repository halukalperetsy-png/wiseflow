using System.Net;
using System.Security.Cryptography;
using CommerceOps.Api.Infrastructure.Persistence;
using CommerceOps.Api.Modules.Identity.Bootstrap;
using CommerceOps.Api.Modules.Identity.Roles;
using CommerceOps.Api.Modules.Identity.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceOps.IntegrationTests;

/// <summary>
/// A host whose configuration carries the first-administrator details, the way
/// User Secrets would locally. The password is generated per run so that nothing
/// password-shaped is committed to this repository.
/// </summary>
public sealed class BootstrapFixture : IdentityFixture
{
    internal string AdminEmail { get; } = UniqueEmail("first-admin");

    internal string AdminPassword { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    internal string AdminDisplayName => "İlk Yönetici";

    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration =>
    [
        .. base.ExtraConfiguration,
        new(BootstrapSettingsResolver.EmailKey, AdminEmail),
        new(BootstrapSettingsResolver.DisplayNameKey, AdminDisplayName),
        new(BootstrapSettingsResolver.PasswordKey, AdminPassword),
    ];
}

/// <summary>
/// Creating the first administrator has to be safe to run again: no duplicate
/// account, and above all no silent password change on an account somebody is
/// already using.
/// </summary>
public sealed class BootstrapAdminTests(BootstrapFixture fixture) : IClassFixture<BootstrapFixture>
{
    [Fact]
    public async Task Running_it_twice_leaves_one_administrator_and_does_not_touch_the_password()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(0, await AdminBootstrapper.RunAsync(fixture.Services, cancellationToken));

        var created = await FindAdministratorAsync(cancellationToken);
        Assert.NotNull(created);
        Assert.True(created.IsActive);

        // Chose the password themselves, so there is nothing to rotate on first use.
        Assert.False(created.MustChangePassword);
        Assert.True(await IsInAdminRoleAsync(created, cancellationToken));

        // The account is usable, and then its owner changes the password.
        using var session = await ApiSession.SignedInAsync(
            fixture, fixture.AdminEmail, fixture.AdminPassword, cancellationToken);

        var ownPassword = "the-owners-own-passphrase";
        var changed = await session.PostAsync(
            "/api/auth/change-password",
            new { currentPassword = fixture.AdminPassword, newPassword = ownPassword },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        // Second run: same configuration, an account that now has a different password.
        Assert.Equal(0, await AdminBootstrapper.RunAsync(fixture.Services, cancellationToken));

        Assert.Equal(1, await CountAdministratorsWithConfiguredEmailAsync(cancellationToken));

        // The owner's password still works -- the second run did not reset it.
        using var afterwards = await ApiSession.SignedInAsync(
            fixture, fixture.AdminEmail, ownPassword, cancellationToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await afterwards.GetAsync("/api/auth/me", cancellationToken)).StatusCode);

        // And the configured password is genuinely gone.
        using var stale = await ApiSession.AnonymousAsync(fixture, cancellationToken);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await stale.SignInAsync(fixture.AdminEmail, fixture.AdminPassword, cancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Running_it_again_restores_an_administrator_who_was_deactivated_or_demoted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(0, await AdminBootstrapper.RunAsync(fixture.Services, cancellationToken));

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByEmailAsync(fixture.AdminEmail);

            user!.IsActive = false;
            await userManager.UpdateAsync(user);
            await userManager.RemoveFromRoleAsync(user, RoleCodes.Admin);
        }

        Assert.Equal(0, await AdminBootstrapper.RunAsync(fixture.Services, cancellationToken));

        var restored = await FindAdministratorAsync(cancellationToken);
        Assert.True(restored!.IsActive);
        Assert.True(await IsInAdminRoleAsync(restored, cancellationToken));
    }

    private Task<User?> FindAdministratorAsync(CancellationToken cancellationToken) =>
        fixture.WithScopeAsync(services => services
            .GetRequiredService<CommerceOpsDbContext>()
            .Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email == fixture.AdminEmail, cancellationToken));

    private Task<int> CountAdministratorsWithConfiguredEmailAsync(CancellationToken cancellationToken) =>
        fixture.WithScopeAsync(services => services
            .GetRequiredService<CommerceOpsDbContext>()
            .Set<User>()
            .AsNoTracking()
            .CountAsync(user => user.Email == fixture.AdminEmail, cancellationToken));

    private Task<bool> IsInAdminRoleAsync(User user, CancellationToken cancellationToken) =>
        fixture.WithScopeAsync(services => services
            .GetRequiredService<CommerceOpsDbContext>()
            .Set<IdentityUserRole<Guid>>()
            .AsNoTracking()
            .AnyAsync(
                assignment => assignment.UserId == user.Id
                    && assignment.RoleId == RoleConfiguration.AdminRoleId,
                cancellationToken));
}
