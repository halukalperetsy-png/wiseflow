import { HealthCard } from '../features/health/HealthCard';
import { PageHeader } from '../shared/ui.tsx';

/**
 * Keeps the Phase 0 health view reachable now that the root screen belongs to
 * product groups. It shows a real, live check -- nothing here is a placeholder.
 */
export function SystemPage() {
  return (
    <div className="co-page">
      <PageHeader title="Sistem" subtitle="API ve veritabanı durumu, beş saniyede bir yenilenir." />
      <HealthCard />
    </div>
  );
}
