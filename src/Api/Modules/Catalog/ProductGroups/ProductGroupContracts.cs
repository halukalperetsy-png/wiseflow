namespace CommerceOps.Api.Modules.Catalog.ProductGroups;

internal sealed record ProductGroupResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record CreateProductGroupRequest(string? Code, string? Name, string? Description);

/// <summary>
/// Every field is optional: an absent field is left alone. There is no delete --
/// retiring a group is IsActive = false.
/// </summary>
internal sealed record UpdateProductGroupRequest(string? Name, string? Description, bool? IsActive);
