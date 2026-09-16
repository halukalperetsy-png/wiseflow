namespace CommerceOps.Api.Modules.Catalog;

/// <summary>
/// Modules map to PostgreSQL schemas. Phase 1 creates this schema for
/// product groups only -- categories and products arrive in Phase 2.
/// </summary>
internal static class CatalogSchema
{
    internal const string Name = "catalog";
}
