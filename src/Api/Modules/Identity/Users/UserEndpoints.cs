using CommerceOps.Api.Infrastructure.ErrorHandling;
using CommerceOps.Api.Infrastructure.Persistence;
using CommerceOps.Api.Modules.Catalog.ProductGroups;
using CommerceOps.Api.Modules.Identity.Authentication;
using CommerceOps.Api.Modules.Identity.Authorization;
using CommerceOps.Api.Modules.Identity.ProductGroupAccess;
using CommerceOps.Api.Modules.Identity.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CommerceOps.Api.Modules.Identity.Users;

/// <summary>
/// User administration. Everything here is under /api/admin because only an
/// administrator ever calls it.
///
/// Two rules run through the whole slice: the acting user is read from the
/// request principal and never from the body, and nothing a client sends -- a
/// role code, a product group id -- is used before it has been checked against
/// the database.
/// </summary>
internal static class UserEndpoints
{
    private const string Route = "/api/admin/users";

    internal static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/admin/roles", ListRolesAsync)
            .RequireAuthorization(Permissions.AdminUserManagement);

        app.MapGet(Route, ListAsync)
            .RequireAuthorization(Permissions.AdminUserManagement);

        app.MapGet($"{Route}/{{id:guid}}", GetAsync)
            .RequireAuthorization(Permissions.AdminUserManagement);

        app.MapPost(Route, CreateAsync)
            .RequireAuthorization(Permissions.AdminUserManagement)
            .ValidateAntiforgery();

        app.MapPatch($"{Route}/{{id:guid}}", UpdateAsync)
            .RequireAuthorization(Permissions.AdminUserManagement)
            .ValidateAntiforgery();

        app.MapPut($"{Route}/{{id:guid}}/roles", SetRolesAsync)
            .RequireAuthorization(Permissions.AdminUserManagement)
            .ValidateAntiforgery();

        app.MapPut($"{Route}/{{id:guid}}/product-groups", SetProductGroupsAsync)
            .RequireAuthorization(Permissions.AdminUserManagement)
            .ValidateAntiforgery();

        app.MapPost($"{Route}/{{id:guid}}/reset-password", ResetPasswordAsync)
            .RequireAuthorization(Permissions.AdminUserManagement)
            .ValidateAntiforgery();

        return app;
    }

    private static async Task<IResult> ListRolesAsync(
        CommerceOpsDbContext context,
        CancellationToken cancellationToken)
    {
        var roles = await context.Set<Role>()
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new RoleResponse(role.Id, role.Name!, role.DisplayName))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(roles);
    }

    private static async Task<IResult> ListAsync(
        CommerceOpsDbContext context,
        CancellationToken cancellationToken)
    {
        var users = await context.Set<User>()
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .Select(user => new UserListItemResponse(
                user.Id,
                user.Email!,
                user.DisplayName,
                user.IsActive,
                user.MustChangePassword,
                context.Set<IdentityUserRole<Guid>>()
                    .Where(assignment => assignment.UserId == user.Id)
                    .Join(
                        context.Set<Role>(),
                        assignment => assignment.RoleId,
                        role => role.Id,
                        (_, role) => role.Name!)
                    .OrderBy(name => name)
                    .ToList(),
                user.CreatedAt))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(users);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        CommerceOpsDbContext context,
        CancellationToken cancellationToken)
    {
        var detail = await LoadDetailAsync(context, id, cancellationToken);

        return detail is null ? ApiProblems.NotFound("Kullanıcı bulunamadı.") : TypedResults.Ok(detail);
    }

    private static async Task<IResult> CreateAsync(
        CreateUserRequest request,
        CommerceOpsDbContext context,
        UserManager<User> userManager,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var email = (request?.Email ?? string.Empty).Trim();
        var displayName = (request?.DisplayName ?? string.Empty).Trim();
        var errors = ValidateProfile(email, displayName);

        var roleCodes = await ResolveRoleCodesAsync(context, request?.Roles, errors, cancellationToken);
        var productGroupIds = await ResolveProductGroupIdsAsync(
            context, request?.ProductGroupIds, errors, cancellationToken);

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        var now = timeProvider.GetUtcNow();
        var temporaryPassword = TemporaryPasswordGenerator.Generate();

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            IsActive = true,

            // The administrator reads this password out once; the user replaces
            // it before they can reach anything else.
            MustChangePassword = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var created = await userManager.CreateAsync(user, temporaryPassword);

        if (!created.Succeeded)
        {
            return created.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName")
                ? ApiProblems.Conflict(ProblemCodes.EmailAlreadyExists, "Bu e-posta adresi zaten kayıtlı.")
                : ApiProblems.Validation(IdentityErrorTranslator.ToValidationErrors(created.Errors, "email"));
        }

        if (roleCodes.Count > 0)
        {
            await userManager.AddToRolesAsync(user, roleCodes);
        }

        await ReplaceProductGroupsAsync(context, user.Id, productGroupIds, now, cancellationToken);

        return TypedResults.Created(
            $"{Route}/{user.Id}",
            new TemporaryPasswordResponse(user.Id, temporaryPassword));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        HttpContext httpContext,
        CommerceOpsDbContext context,
        UserManager<User> userManager,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());

        if (user is null)
        {
            return ApiProblems.NotFound("Kullanıcı bulunamadı.");
        }

        if (request?.IsActive is false && user.Id == httpContext.User.GetRequiredUserId())
        {
            return ApiProblems.Forbidden(
                ProblemCodes.SelfDeactivationForbidden,
                "Kendi hesabınızı pasifleştiremezsiniz.");
        }

        var displayName = request?.DisplayName is null ? user.DisplayName : request.DisplayName.Trim();

        if (displayName.Length is < UserRules.DisplayNameMinLength or > UserRules.DisplayNameMaxLength)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["displayName"] = [UserRules.DisplayNameRequirement],
            });
        }

        var deactivating = request?.IsActive is false && user.IsActive;

        var failure = await LastAdminGuard.RunAsync(
            context,
            async () =>
            {
                user.DisplayName = displayName;

                if (request?.IsActive is { } isActive)
                {
                    user.IsActive = isActive;
                }

                user.UpdatedAt = timeProvider.GetUtcNow();
                await userManager.UpdateAsync(user);

                if (deactivating)
                {
                    // Ends every session this user has, on its next request.
                    await userManager.UpdateSecurityStampAsync(user);
                }

                return null;
            },
            cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        return TypedResults.Ok((await LoadDetailAsync(context, id, cancellationToken))!);
    }

    private static async Task<IResult> SetRolesAsync(
        Guid id,
        SetRolesRequest request,
        HttpContext httpContext,
        CommerceOpsDbContext context,
        UserManager<User> userManager,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (id == httpContext.User.GetRequiredUserId())
        {
            return ApiProblems.Forbidden(
                ProblemCodes.SelfRoleChangeForbidden,
                "Kendi rollerinizi değiştiremezsiniz.");
        }

        var user = await userManager.FindByIdAsync(id.ToString());

        if (user is null)
        {
            return ApiProblems.NotFound("Kullanıcı bulunamadı.");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var roleCodes = await ResolveRoleCodesAsync(context, request?.Roles, errors, cancellationToken);

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        var failure = await LastAdminGuard.RunAsync(
            context,
            async () =>
            {
                var current = await userManager.GetRolesAsync(user);
                var removed = current.Except(roleCodes, StringComparer.Ordinal).ToArray();
                var added = roleCodes.Except(current, StringComparer.Ordinal).ToArray();

                if (removed.Length > 0)
                {
                    await userManager.RemoveFromRolesAsync(user, removed);
                }

                if (added.Length > 0)
                {
                    await userManager.AddToRolesAsync(user, added);
                }

                if (removed.Length > 0 || added.Length > 0)
                {
                    user.UpdatedAt = timeProvider.GetUtcNow();
                    await userManager.UpdateAsync(user);
                }

                return null;
            },
            cancellationToken);

        return failure ?? TypedResults.NoContent();
    }

    private static async Task<IResult> SetProductGroupsAsync(
        Guid id,
        SetProductGroupsRequest request,
        HttpContext httpContext,
        CommerceOpsDbContext context,
        UserManager<User> userManager,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (id == httpContext.User.GetRequiredUserId())
        {
            return ApiProblems.Forbidden(
                ProblemCodes.SelfRoleChangeForbidden,
                "Kendi ürün grubu erişiminizi değiştiremezsiniz.");
        }

        var user = await userManager.FindByIdAsync(id.ToString());

        if (user is null)
        {
            return ApiProblems.NotFound("Kullanıcı bulunamadı.");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var productGroupIds = await ResolveProductGroupIdsAsync(
            context, request?.ProductGroupIds, errors, cancellationToken);

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        await ReplaceProductGroupsAsync(
            context, user.Id, productGroupIds, timeProvider.GetUtcNow(), cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> ResetPasswordAsync(
        Guid id,
        UserManager<User> userManager,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());

        if (user is null)
        {
            return ApiProblems.NotFound("Kullanıcı bulunamadı.");
        }

        var temporaryPassword = TemporaryPasswordGenerator.Generate();

        await userManager.RemovePasswordAsync(user);
        var added = await userManager.AddPasswordAsync(user, temporaryPassword);

        if (!added.Succeeded)
        {
            return ApiProblems.Validation(
                IdentityErrorTranslator.ToValidationErrors(added.Errors, "newPassword"));
        }

        user.MustChangePassword = true;
        user.UpdatedAt = timeProvider.GetUtcNow();
        await userManager.UpdateAsync(user);

        // A reset is a security event: every session this user has ends.
        await userManager.UpdateSecurityStampAsync(user);

        return TypedResults.Ok(new TemporaryPasswordResponse(user.Id, temporaryPassword));
    }

    private static Dictionary<string, string[]> ValidateProfile(string email, string displayName)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var emailLooksValid = email.Length > 0
            && email.Length <= UserRules.EmailMaxLength
            && email.Contains('@', StringComparison.Ordinal);

        if (!emailLooksValid)
        {
            errors["email"] = ["Geçerli bir e-posta adresi girin."];
        }

        if (displayName.Length is < UserRules.DisplayNameMinLength or > UserRules.DisplayNameMaxLength)
        {
            errors["displayName"] = [UserRules.DisplayNameRequirement];
        }

        return errors;
    }

    /// <summary>Role codes are checked against the table; a client cannot invent one.</summary>
    private static async Task<IReadOnlyList<string>> ResolveRoleCodesAsync(
        CommerceOpsDbContext context,
        string[]? requested,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        var codes = (requested ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (codes.Length == 0)
        {
            return [];
        }

        var known = await context.Set<Role>()
            .AsNoTracking()
            .Where(role => codes.Contains(role.Name!))
            .Select(role => role.Name!)
            .ToListAsync(cancellationToken);

        if (known.Count != codes.Length)
        {
            errors["roles"] = ["Geçersiz rol seçildi."];
        }

        return known;
    }

    /// <summary>Product group ids are checked against the table for the same reason.</summary>
    private static async Task<IReadOnlyList<Guid>> ResolveProductGroupIdsAsync(
        CommerceOpsDbContext context,
        Guid[]? requested,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        var ids = (requested ?? []).Distinct().ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var known = await context.Set<ProductGroup>()
            .AsNoTracking()
            .Where(group => ids.Contains(group.Id))
            .Select(group => group.Id)
            .ToListAsync(cancellationToken);

        if (known.Count != ids.Length)
        {
            errors["productGroupIds"] = ["Geçersiz ürün grubu seçildi."];
        }

        return known;
    }

    private static async Task ReplaceProductGroupsAsync(
        CommerceOpsDbContext context,
        Guid userId,
        IReadOnlyList<Guid> productGroupIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await context.Set<UserProductGroup>()
            .Where(assignment => assignment.UserId == userId)
            .ToListAsync(cancellationToken);

        context.Set<UserProductGroup>().RemoveRange(
            existing.Where(assignment => !productGroupIds.Contains(assignment.ProductGroupId)));

        var additions = productGroupIds
            .Where(id => !existing.Exists(assignment => assignment.ProductGroupId == id))
            .Select(id => new UserProductGroup
            {
                UserId = userId,
                ProductGroupId = id,
                AssignedAt = now,
            });

        context.Set<UserProductGroup>().AddRange(additions);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Task<UserDetailResponse?> LoadDetailAsync(
        CommerceOpsDbContext context,
        Guid id,
        CancellationToken cancellationToken) =>
        context.Set<User>()
            .AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => new UserDetailResponse(
                user.Id,
                user.Email!,
                user.DisplayName,
                user.IsActive,
                user.MustChangePassword,
                context.Set<IdentityUserRole<Guid>>()
                    .Where(assignment => assignment.UserId == user.Id)
                    .Join(
                        context.Set<Role>(),
                        assignment => assignment.RoleId,
                        role => role.Id,
                        (_, role) => role.Name!)
                    .OrderBy(name => name)
                    .ToList(),
                context.Set<UserProductGroup>()
                    .Where(assignment => assignment.UserId == user.Id)
                    .Select(assignment => assignment.ProductGroupId)
                    .ToList(),
                user.CreatedAt,
                user.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
}
