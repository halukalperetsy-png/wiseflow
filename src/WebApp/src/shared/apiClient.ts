/**
 * The one way this app talks to /api.
 *
 * It does three things no call site should have to repeat: it carries the CSRF
 * request token on every unsafe request, it turns an RFC 7807 problem document
 * into a value rather than an exception, and it reports a session that has gone
 * away so the shell can react once instead of every screen guessing.
 */

export const REQUEST_TIMEOUT_MS = 10_000;

export const CSRF_HEADER = 'X-CSRF-TOKEN';

export interface ApiProblem {
  status: number;
  /** Stable English code; messages.ts maps it to Turkish. */
  code: string;
  /** Human-readable fallback from the server, already in Turkish. */
  title: string;
  /** Field name to messages, present on validation failures. */
  errors?: Record<string, string[]>;
}

export type ApiResult<T> = { ok: true; data: T } | { ok: false; problem: ApiProblem };

type Unsafe = 'POST' | 'PUT' | 'PATCH' | 'DELETE';

/**
 * The antiforgery token is bound to the caller's identity, so it is dropped
 * after every sign-in and sign-out and fetched again on the next write.
 */
let csrfToken: string | null = null;

let sessionLostHandler: (() => void) | null = null;

export function forgetCsrfToken(): void {
  csrfToken = null;
}

export function onSessionLost(handler: (() => void) | null): void {
  sessionLostHandler = handler;
}

export async function apiGet<T>(path: string, signal?: AbortSignal): Promise<ApiResult<T>> {
  return request<T>('GET', path, undefined, signal);
}

export async function apiSend<T>(
  method: Unsafe,
  path: string,
  body?: unknown,
  signal?: AbortSignal,
): Promise<ApiResult<T>> {
  const first = await request<T>(method, path, body, signal);

  // A token can go stale between screens -- the identity it is bound to changed
  // somewhere else in the tab. One silent refresh, then the answer stands.
  if (!first.ok && first.problem.code === 'csrf_failed') {
    forgetCsrfToken();
    return request<T>(method, path, body, signal);
  }

  return first;
}

async function request<T>(
  method: string,
  path: string,
  body: unknown,
  signal?: AbortSignal,
): Promise<ApiResult<T>> {
  const headers: Record<string, string> = { Accept: 'application/json' };

  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  if (method !== 'GET') {
    const token = await ensureCsrfToken(signal);

    if (token === null) {
      return { ok: false, problem: unreachableProblem() };
    }

    headers[CSRF_HEADER] = token;
  }

  let response: Response;

  try {
    response = await fetch(path, {
      method,
      headers,
      cache: 'no-store',
      credentials: 'same-origin',
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: signal ?? AbortSignal.timeout(REQUEST_TIMEOUT_MS),
    });
  } catch {
    return { ok: false, problem: unreachableProblem() };
  }

  if (response.status === 401 && reportsSessionLoss(path)) {
    sessionLostHandler?.();
  }

  if (response.status === 204) {
    return { ok: true, data: undefined as T };
  }

  if (!response.ok) {
    return { ok: false, problem: await readProblem(response) };
  }

  try {
    return { ok: true, data: (await response.json()) as T };
  } catch {
    return {
      ok: false,
      problem: { status: response.status, code: 'invalid_response', title: 'Sunucudan beklenmeyen bir yanıt geldi.' },
    };
  }
}

/**
 * Signing in and reading the session are how the app finds out whether anyone
 * is signed in, so a 401 from those is an answer, not a lost session.
 */
function reportsSessionLoss(path: string): boolean {
  return path !== '/api/auth/login' && path !== '/api/auth/me' && path !== '/api/auth/csrf';
}

async function ensureCsrfToken(signal?: AbortSignal): Promise<string | null> {
  if (csrfToken !== null) {
    return csrfToken;
  }

  const result = await request<{ token: string }>('GET', '/api/auth/csrf', undefined, signal);

  if (!result.ok || typeof result.data?.token !== 'string' || result.data.token.length === 0) {
    return null;
  }

  csrfToken = result.data.token;
  return csrfToken;
}

async function readProblem(response: Response): Promise<ApiProblem> {
  let payload: unknown = null;

  try {
    payload = await response.json();
  } catch {
    payload = null;
  }

  const document = isRecord(payload) ? payload : {};

  return {
    status: response.status,
    code: typeof document['code'] === 'string' ? document['code'] : fallbackCode(response.status),
    title: typeof document['title'] === 'string' ? document['title'] : '',
    errors: readErrors(document['errors']),
  };
}

function readErrors(value: unknown): Record<string, string[]> | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const errors: Record<string, string[]> = {};

  for (const [field, messages] of Object.entries(value)) {
    if (Array.isArray(messages) && messages.every((message) => typeof message === 'string')) {
      errors[field] = messages;
    }
  }

  return Object.keys(errors).length > 0 ? errors : undefined;
}

function fallbackCode(status: number): string {
  switch (status) {
    case 400:
      return 'validation_failed';
    case 401:
      return 'unauthorized';
    case 403:
      return 'forbidden';
    case 404:
      return 'not_found';
    case 409:
      return 'conflict';
    case 429:
      return 'too_many_requests';
    default:
      return 'server_error';
  }
}

function unreachableProblem(): ApiProblem {
  return { status: 0, code: 'unreachable', title: 'Sunucuya ulaşılamadı.' };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
