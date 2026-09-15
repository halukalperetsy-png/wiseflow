export interface HealthCheck {
  name: string;
  status: string;
  durationMs: number;
}

export interface HealthReport {
  status: string;
  durationMs: number;
  checks: HealthCheck[];
}

/**
 * Six distinct outcomes. The important distinction is `unhealthy` -- an HTTP 503
 * carrying a VALID body means the API is fine and is reporting a failed
 * dependency. That is not the same as the API being unreachable.
 */
export type HealthProbe =
  | { kind: 'healthy'; report: HealthReport; at: number }
  | { kind: 'degraded'; report: HealthReport; at: number }
  | { kind: 'unhealthy'; report: HealthReport; at: number }
  | { kind: 'unreachable'; at: number }
  | { kind: 'timeout'; at: number }
  | { kind: 'invalid'; at: number };
