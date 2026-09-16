using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommerceOps.Api.Modules.Identity;

// ASP.NET Core Identity brings its own join tables. Four of them
// (user_claims, user_logins, user_tokens, role_claims) are part of the
// framework model and are never written or read by Phase 1 code -- they serve
// per-user claims, external logins, email/MFA tokens and data-driven role
// claims, all of which are out of scope. They are mapped into the identity
// schema with the project's naming so the schema stays uniform.

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("user_roles", IdentitySchema.Name);
    }
}

internal sealed class UserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("user_claims", IdentitySchema.Name);
    }
}

internal sealed class UserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("user_logins", IdentitySchema.Name);
    }
}

internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("user_tokens", IdentitySchema.Name);
    }
}

internal sealed class RoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("role_claims", IdentitySchema.Name);
    }
}
