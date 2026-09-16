using CommerceOps.Api.Modules.Catalog.ProductGroups;
using CommerceOps.Api.Modules.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommerceOps.Api.Modules.Identity.ProductGroupAccess;

internal sealed class UserProductGroupConfiguration : IEntityTypeConfiguration<UserProductGroup>
{
    public void Configure(EntityTypeBuilder<UserProductGroup> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user_product_groups", IdentitySchema.Name);

        builder.HasKey(assignment => new { assignment.UserId, assignment.ProductGroupId });

        builder.Property(assignment => assignment.AssignedAt).IsRequired();

        // No navigation properties: the assignment is only ever read through an
        // explicit join, and keeping it free of navigations stops the catalog
        // module from acquiring an identity-shaped graph.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ProductGroup>()
            .WithMany()
            .HasForeignKey(assignment => assignment.ProductGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(assignment => assignment.ProductGroupId);
    }
}
