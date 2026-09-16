using System.Security.Claims;
using CommerceOps.Api.Infrastructure.ErrorHandling;
using CommerceOps.Api.Infrastructure.Persistence;
using CommerceOps.Api.Modules.Identity.Authentication;
using CommerceOps.Api.Modules.Identity.Authorization;
using CommerceOps.Api.Modules.Identity.ProductGroupAccess;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CommerceOps.Api.Modules.Catalog.ProductGroups;

/// <summary>
/// Product groups: the unit of data authorization, and in Phase 1 the only
/// catalog record. Reading is scoped per user; creating and changing need the
/// administration permission.
/// </summary>
internal static class ProductGroupEndpoints
{
    private const string Route = "/api/product-groups";

    internal static IEndpointRouteBuilder MapProductGroupEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, ListAsync)
            .RequireAuthorization(Permissions.ProductGroupView);

        app.MapGet($"{Route}/{{id:guid}}", GetAsync)
            .RequireAuthorization(Permissions.ProductGroupView);

        app.MapPost(Route, CreateAsync)
            .RequireAuthorization(Permissions.AdminProductGroupManagement)
            .ValidateAntiforgery();

        app.MapPatch($"{Route}/{{id:guid}}", UpdateAsync)
            .RequireAuthorization(Permissions.AdminProductGroupManagement)
            .ValidateAntiforgery();

        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        CommerceOpsDbContext context,
        CancellationToken cancellationToken)
    {
        var groups = await ProductGroupScope.Visible(context, httpContext.User)
            .AsNoTracking()
            .OrderBy(group => group.Code)
            .Select(group => ToResponse(group))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(groups);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        HttpContext httpContext,
        CommerceOpsDbContext context,
        CancellationToken cancellationToken)
    {
        var group = await ProductGroupScope.Visible(context, httpContext.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return group is null
            ? ApiProblems.NotFound("Ürün grubu bulunamadı.")
            : TypedResults.Ok(ToResponse(group));
    }

    private static async Task<IResult> CreateAsync(
        CreateProductGroupRequest request,
        CommerceOpsDbContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var code = ProductGroupRules.NormalizeCode(request?.Code ?? string.Empty);
        var name = (request?.Name ?? string.Empty).Trim();
        var errors = Validate(code, name, request?.Description);

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        var taken = await context.Set<ProductGroup>()
            .AnyAsync(group => group.Code == code, cancellationToken);

        if (taken)
        {
            return CodeConflict();
        }

        var now = timeProvider.GetUtcNow();

        var created = new ProductGroup
        {
            Id = Guid.CreateVersion7(),
            Code = code,
            Name = name,
            Description = Normalize(request?.Description),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        context.Set<ProductGroup>().Add(created);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Two administrators submitting the same code at the same moment.
            return CodeConflict();
        }

        return TypedResults.Created($"{Route}/{created.Id}", ToResponse(created));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateProductGroupRequest request,
        HttpContext httpContext,
        CommerceOpsDbContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // Same scope filter as the read paths -- not a second, hand-written one.
        var group = await ProductGroupScope.FindVisibleAsync(context, httpContext.User, id, cancellationToken);

        if (group is null)
        {
            return ApiProblems.NotFound("Ürün grubu bulunamadı.");
        }

        var name = request?.Name is null ? group.Name : request.Name.Trim();
        var errors = Validate(group.Code, name, request?.Description);

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        group.Name = name;

        if (request?.Description is not null)
        {
            group.Description = Normalize(request.Description);
        }

        if (request?.IsActive is { } isActive)
        {
            group.IsActive = isActive;
        }

        group.UpdatedAt = timeProvider.GetUtcNow();

        await context.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(ToResponse(group));
    }

    private static Dictionary<string, string[]> Validate(string code, string name, string? description)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!ProductGroupRules.IsValidCode(code))
        {
            errors["code"] =
            [
                $"Kod {ProductGroupRules.CodeMinLength}-{ProductGroupRules.CodeMaxLength} karakter olmalı ve "
                + "yalnız harf, rakam ve tire içermelidir.",
            ];
        }

        if (name.Length == 0 || name.Length > ProductGroupRules.NameMaxLength)
        {
            errors["name"] = [$"Ad zorunludur ve en fazla {ProductGroupRules.NameMaxLength} karakter olabilir."];
        }

        if (description is { Length: > ProductGroupRules.DescriptionMaxLength })
        {
            errors["description"] =
                [$"Açıklama en fazla {ProductGroupRules.DescriptionMaxLength} karakter olabilir."];
        }

        return errors;
    }

    private static IResult CodeConflict() => ApiProblems.Conflict(
        ProblemCodes.ProductGroupCodeAlreadyExists,
        "Bu kod başka bir ürün grubunda kullanılıyor.");

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProductGroupResponse ToResponse(ProductGroup group) => new(
        group.Id,
        group.Code,
        group.Name,
        group.Description,
        group.IsActive,
        group.CreatedAt,
        group.UpdatedAt);
}
