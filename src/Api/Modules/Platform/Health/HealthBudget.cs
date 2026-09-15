namespace CommerceOps.Api.Modules.Platform.Health;

/// <summary>
/// Time budget for health checking.
///
/// The limit applies to the health check registration only. Ordinary DbContext
/// work and EF migrations are deliberately left unbounded -- a schema change may
/// legitimately run longer than a liveness probe should wait.
///
/// It must stay below the frontend request timeout (3s, see healthApi.ts) so a
/// failing dependency reaches the browser as a valid 503 body rather than as a
/// client-side timeout.
/// </summary>
internal static class HealthBudget
{
    internal static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(2);
}
