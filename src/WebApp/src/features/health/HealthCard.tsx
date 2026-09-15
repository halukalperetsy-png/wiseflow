import { useEffect, useState } from 'react';
import { POLL_INTERVAL_MS, REQUEST_TIMEOUT_MS, probeHealth } from './healthApi';
import type { HealthProbe } from './types';

function useHealthProbe(): HealthProbe | null {
  const [probe, setProbe] = useState<HealthProbe | null>(null);

  useEffect(() => {
    const unmount = new AbortController();
    let inFlight = false;

    const tick = async () => {
      // Non-overlapping: a tick that arrives while a request is open is
      // skipped outright rather than queued.
      if (inFlight || unmount.signal.aborted) {
        return;
      }
      inFlight = true;
      try {
        const signal = AbortSignal.any([
          AbortSignal.timeout(REQUEST_TIMEOUT_MS),
          unmount.signal,
        ]);
        const result = await probeHealth(signal);
        if (!unmount.signal.aborted) {
          setProbe(result);
        }
      } catch {
        /* unmount-driven AbortError only */
      } finally {
        inFlight = false;
      }
    };

    void tick();
    const timer = window.setInterval(() => void tick(), POLL_INTERVAL_MS);

    return () => {
      unmount.abort();
      window.clearInterval(timer);
    };
  }, []);

  return probe;
}

interface Presentation {
  icon: string;
  label: string;
  detail: string;
  color: string;
}

function present(probe: HealthProbe | null): Presentation {
  if (probe === null) {
    return {
      icon: '◌',
      label: 'Checking',
      detail: 'Contacting the API...',
      color: 'var(--color-text-muted)',
    };
  }

  switch (probe.kind) {
    case 'healthy':
      return {
        icon: '✓',
        label: 'Healthy',
        detail: 'The API and all its dependencies are responding.',
        color: 'var(--color-success)',
      };
    case 'degraded':
      return {
        icon: '!',
        label: 'Degraded',
        detail: 'The API is up but at least one check is degraded.',
        color: 'var(--color-warning)',
      };
    case 'unhealthy':
      return {
        icon: '×',
        label: 'Unhealthy',
        detail: 'The API answered (HTTP 503) and reports a failing dependency.',
        color: 'var(--color-danger)',
      };
    case 'unreachable':
      return {
        icon: '⚡',
        label: 'Unreachable',
        detail: 'No response from the API. Is it running on port 5080?',
        color: 'var(--color-danger)',
      };
    case 'timeout':
      return {
        icon: '◷',
        label: 'Timed out',
        detail: `No answer within ${REQUEST_TIMEOUT_MS / 1000}s.`,
        color: 'var(--color-warning)',
      };
    case 'invalid':
      return {
        icon: '?',
        label: 'Invalid response',
        detail: 'The API answered but the payload did not match the contract.',
        color: 'var(--color-warning)',
      };
  }
}

const cardStyle: React.CSSProperties = {
  background: 'var(--color-surface-raised)',
  border: '1px solid var(--color-border)',
  borderRadius: 'var(--radius)',
  padding: 'var(--space-5)',
  maxWidth: '560px',
};

export function HealthCard() {
  const probe = useHealthProbe();
  const view = present(probe);
  const report = probe !== null && 'report' in probe ? probe.report : null;

  return (
    <section style={cardStyle} aria-live="polite">
      <h2 style={{ margin: 0, fontSize: '13px', textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--color-text-muted)' }}>
        API health
      </h2>

      {/* Status is conveyed by icon AND text, never by colour alone. */}
      <p style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)', margin: 'var(--space-3) 0 var(--space-2)', fontSize: '20px', fontWeight: 600, color: view.color }}>
        <span aria-hidden="true">{view.icon}</span>
        <span>{view.label}</span>
      </p>

      <p style={{ margin: 0, color: 'var(--color-text-muted)' }}>{view.detail}</p>

      {report !== null && (
        <table style={{ marginTop: 'var(--space-4)', width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
          <thead>
            <tr style={{ textAlign: 'left', color: 'var(--color-text-muted)' }}>
              <th style={{ padding: 'var(--space-1) 0' }}>Check</th>
              <th style={{ padding: 'var(--space-1) 0' }}>Status</th>
              <th style={{ padding: 'var(--space-1) 0', textAlign: 'right' }}>Duration</th>
            </tr>
          </thead>
          <tbody>
            {report.checks.map((check) => (
              <tr key={check.name} style={{ borderTop: '1px solid var(--color-border)' }}>
                <td style={{ padding: 'var(--space-2) 0' }}>
                  <code>{check.name}</code>
                </td>
                <td style={{ padding: 'var(--space-2) 0' }}>
                  <span aria-hidden="true">{check.status === 'Healthy' ? '✓' : '×'}</span> {check.status}
                </td>
                <td style={{ padding: 'var(--space-2) 0', textAlign: 'right', color: 'var(--color-text-muted)' }}>
                  {check.durationMs} ms
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <p style={{ marginTop: 'var(--space-4)', marginBottom: 0, fontSize: '12px', color: 'var(--color-text-muted)' }}>
        {probe === null
          ? `Polling every ${POLL_INTERVAL_MS / 1000}s`
          : `Last checked ${new Date(probe.at).toLocaleTimeString()} - every ${POLL_INTERVAL_MS / 1000}s`}
      </p>
    </section>
  );
}
