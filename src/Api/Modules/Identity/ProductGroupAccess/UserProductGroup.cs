namespace CommerceOps.Api.Modules.Identity.ProductGroupAccess;

/// <summary>
/// Which product groups a user may work in. This is authorization data, not
/// catalog data, so it lives in the identity module and the identity schema.
///
/// There is deliberately no access level column: Phase 1 has no operation that
/// distinguishes reading from writing inside a group, so the column would have
/// no consumer. It arrives with product writes in Phase 2.
/// </summary>
internal sealed class UserProductGroup
{
    public Guid UserId { get; set; }

    public Guid ProductGroupId { get; set; }

    public DateTimeOffset AssignedAt { get; set; }
}
