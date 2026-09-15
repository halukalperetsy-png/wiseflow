import type { HealthCheck, HealthProbe, HealthReport } from './types';

export const POLL_INTERVAL_MS = 5000;
/** Must stay above the API health budget (2s) and below the poll interval. */
export const REQUEST_TIMEOUT_MS = 3000;

const VALID_STATUSES = ['Healthy', 'Degraded', 'Unhealthy'] as const;
type HealthStatus = (typeof VALID_STATUSES)[number];

/** The API emits exactly these pairings; anything else is an inconsistent response. */
const EXPECTED_HTTP: Record<HealthStatus, number> = {
  Healthy: 200,
  Degraded: 200,
  Unhealthy: 503,
};

function isHealthStatus(value: unknown): value is HealthStatus {
  return typeof value === 'string' && (VALID_STATUSES as readonly string[]).includes(value);
}

function isCheck(value: unknown): value is HealthCheck {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const check = value as Record<string, unknown>;
  return (
    typeof check.name === 'string' &&
    // A nested status outside the known set breaks the contract just as much
    // as a missing field, so it is rejected here rather than rendered.
    isHealthStatus(check.status) &&
    typeof check.durationMs === 'number'
  );
}

function isHealthReport(value: unknown): value is HealthReport {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const report = value as Record<string, unknown>;
  return (
    isHealthStatus(report.status) &&
    typeof report.durationMs === 'number' &&
    Array.isArray(report.checks) &&
    report.checks.every(isCheck)
  );
}

/**
 * Distinguishes a request timeout from an unmount abort; null for anything else.
 *
 * The signal is consulted first. A composite signal built with AbortSignal.any
 * carries the reason of whichever source fired, and that reason survives even
 * when the failure surfaces as a plain AbortError -- which is what happens when
 * the cancellation lands while the response body is being read. Reading only
 * the error name there would misread a timeout as an unmount and swallow it,
 * leaving the card stuck on its previous result.
 */
function abortKind(signal: AbortSignal, error: unknown): 'timeout' | 'unmount' | null {
  if (signal.aborted) {
    const reason: unknown = signal.reason;
    if (reason instanceof DOMException && reason.name === 'TimeoutError') {
      return 'timeout';
    }
    return 'unmount';
  }

  // Not aborted: fall back to the error itself.
  if (error instanceof DOMException) {
    if (error.name === 'TimeoutError') {
      return 'timeout';
    }
    if (error.name === 'AbortError') {
      return 'unmount';
    }
  }

  return null;
}

export async function probeHealth(signal: AbortSignal): Promise<HealthProbe> {
  const at = Date.now();

  let response: Response;
  try {
    response = await fetch('/health', {
      signal,
      cache: 'no-store',
      headers: { Accept: 'application/json' },
    });
  } catch (error) {
    const kind = abortKind(signal, error);
    if (kind === 'unmount') {
      throw error;
    }
    if (kind === 'timeout') {
      return { kind: 'timeout', at };
    }
    return { kind: 'unreachable', at };
  }

  // The API only ever answers 200 or 503. Any other code came from the dev
  // proxy failing to reach it, so the API is effectively unreachable.
  if (response.status !== 200 && response.status !== 503) {
    return { kind: 'unreachable', at };
  }

  let body: unknown;
  try {
    body = await response.json();
  } catch (error) {
    const kind = abortKind(signal, error);
    if (kind === 'unmount') {
      throw error;
    }
    if (kind === 'timeout') {
      return { kind: 'timeout', at };
    }
    return { kind: 'invalid', at };
  }

  if (!isHealthReport(body)) {
    return { kind: 'invalid', at };
  }

  if (!VALID_STATUSES.includes(body.status as HealthStatus)) {
    return { kind: 'invalid', at };
  }
  const status = body.status as HealthStatus;

  // Status code and body status are validated together.
  if (response.status !== EXPECTED_HTTP[status]) {
    return { kind: 'invalid', at };
  }

  if (status === 'Healthy') {
    return { kind: 'healthy', report: body, at };
  }
  if (status === 'Degraded') {
    return { kind: 'degraded', report: body, at };
  }
  return { kind: 'unhealthy', report: body, at };
}
