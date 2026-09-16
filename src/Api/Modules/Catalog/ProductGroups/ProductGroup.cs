namespace CommerceOps.Api.Modules.Catalog.ProductGroups;

/// <summary>
/// A business operating group. Every user sees only the product groups they are
/// assigned to; Phase 2 hangs categories and products off this record.
///
/// Records are never deleted -- <see cref="IsActive"/> retires them instead.
/// </summary>
internal sealed class ProductGroup
{
    public Guid Id { get; set; }

    /// <summary>Stable short identifier, stored upper-case so lookups never depend on casing.</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
