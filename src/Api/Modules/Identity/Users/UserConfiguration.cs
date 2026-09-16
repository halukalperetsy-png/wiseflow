using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommerceOps.Api.Modules.Identity.Users;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Identity's default is AspNetUsers in the default schema.
        builder.ToTable("users", IdentitySchema.Name);

        builder.Property(user => user.DisplayName).HasMaxLength(UserRules.DisplayNameMaxLength).IsRequired();
        builder.Property(user => user.IsActive).IsRequired();
        builder.Property(user => user.MustChangePassword).IsRequired();
        builder.Property(user => user.CreatedAt).IsRequired();
        builder.Property(user => user.UpdatedAt).IsRequired();

        // Identity allows a null email; this application signs in with it.
        builder.Property(user => user.Email).IsRequired();
        builder.Property(user => user.NormalizedEmail).IsRequired();

        // Identity's own EmailIndex is not unique -- RequireUniqueEmail is only
        // checked by UserValidator. Enforce it in the database as well.
        builder.HasIndex(user => user.NormalizedEmail).IsUnique();
    }
}
