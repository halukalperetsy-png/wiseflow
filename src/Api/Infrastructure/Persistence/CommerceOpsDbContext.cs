using Microsoft.EntityFrameworkCore;

namespace CommerceOps.Api.Infrastructure.Persistence;

/// <summary>
/// Phase 0 holds no entities. The assembly scan is in place so the first real
/// entity in Phase 1 only needs its own IEntityTypeConfiguration.
/// </summary>
internal sealed class CommerceOpsDbContext(DbContextOptions<CommerceOpsDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceOpsDbContext).Assembly);
    }
}
