using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommerceOps.Api.Modules.Catalog.ProductGroups;

internal sealed class ProductGroupConfiguration : IEntityTypeConfiguration<ProductGroup>
{
    public void Configure(EntityTypeBuilder<ProductGroup> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("product_groups", CatalogSchema.Name);

        builder.HasKey(group => group.Id);

        builder.Property(group => group.Code).HasMaxLength(ProductGroupRules.CodeMaxLength).IsRequired();
        builder.Property(group => group.Name).HasMaxLength(ProductGroupRules.NameMaxLength).IsRequired();
        builder.Property(group => group.Description).HasMaxLength(ProductGroupRules.DescriptionMaxLength);
        builder.Property(group => group.IsActive).IsRequired();
        builder.Property(group => group.CreatedAt).IsRequired();
        builder.Property(group => group.UpdatedAt).IsRequired();

        // Codes are stored upper-case, so a plain unique index is enough.
        builder.HasIndex(group => group.Code).IsUnique();
    }
}
