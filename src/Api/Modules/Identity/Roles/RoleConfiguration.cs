using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommerceOps.Api.Modules.Identity.Roles;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <summary>
    /// Fixed identifiers and concurrency stamps: HasData must produce the same
    /// migration every time it is scaffolded.
    /// </summary>
    internal static readonly Guid AdminRoleId = new("a1000000-0000-4000-8000-000000000001");

    internal static readonly Guid ViewerRoleId = new("a1000000-0000-4000-8000-000000000002");

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("roles", IdentitySchema.Name);

        builder.Property(role => role.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(role => role.Name).IsRequired();
        builder.Property(role => role.NormalizedName).IsRequired();

        // Seeded here rather than by the bootstrap command so that every
        // database -- including a throwaway test one -- has them after migrating.
        builder.HasData(
            new Role
            {
                Id = AdminRoleId,
                Name = RoleCodes.Admin,
                NormalizedName = RoleCodes.Admin.ToUpperInvariant(),
                DisplayName = "Yönetici",
                ConcurrencyStamp = "a1000000-0000-4000-8000-000000000001",
            },
            new Role
            {
                Id = ViewerRoleId,
                Name = RoleCodes.Viewer,
                NormalizedName = RoleCodes.Viewer.ToUpperInvariant(),
                DisplayName = "Görüntüleyici",
                ConcurrencyStamp = "a1000000-0000-4000-8000-000000000002",
            });
    }
}
