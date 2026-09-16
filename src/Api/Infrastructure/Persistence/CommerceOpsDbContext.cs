using CommerceOps.Api.Modules.Identity.Roles;
using CommerceOps.Api.Modules.Identity.Users;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CommerceOps.Api.Infrastructure.Persistence;

/// <summary>
/// One DbContext for the whole application (ADR-001).
///
/// The base type is IdentityDbContext so ASP.NET Core Identity's EF stores work
/// unchanged. That is a deliberate, visible coupling from Infrastructure to the
/// Identity module -- documented in ADR-003 -- and the only one: modules add no
/// DbSet properties here, they use context.Set&lt;T&gt;() and an
/// IEntityTypeConfiguration that the assembly scan below picks up.
/// </summary>
internal sealed class CommerceOpsDbContext(DbContextOptions<CommerceOpsDbContext> options)
    : IdentityDbContext<User, Role, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceOpsDbContext).Assembly);

        // Last, so it sees every name the configurations above produced.
        SnakeCaseNaming.Apply(modelBuilder);
    }
}
