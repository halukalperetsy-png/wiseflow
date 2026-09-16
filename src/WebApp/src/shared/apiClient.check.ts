/*
 * Checks for the parts of apiClient that decide whether a request is safe and
 * whether a failure is the user's problem or the session's.
 *
 * Run with:  npm run check:api
 *
 * Dependency-free on purpose, the same way healthApi.check.ts is: Node 24 runs
 * TypeScript directly, fetch is stubbed, and throwing at the end is what makes
 * the exit code non-zero.
 */

import { apiGet, apiSend, CSRF_HEADER, forgetCsrfToken, onSessionLost } from './apiClient.ts';
import { describeProblem, firstFieldError } from './messages.ts';

let failures = 0;

function check(name: string, passed: boolean, detail = ''): void {
  if (passed) {
    console.log(`  PASS  ${name}`);
    return;
  }
  failures += 1;
  console.error(`  FAIL  ${name}${detail ? ` -- ${detail}` : ''}`);
}

interface Call {
  path: string;
  method: string;
  headers: Record<string, string>;
  body: string | undefined;
}

interface Reply {
  status: number;
  payload?: unknown;
}

const calls: Call[] = [];

/** Answers each call from a queue, recording what was sent. */
function stubFetch(replies: Reply[]): void {
  const queue = [...replies];

  globalThis.fetch = ((path: string, init: RequestInit) => {
    calls.push({
      path,
      method: init.method ?? 'GET',
      headers: (init.headers ?? {}) as Record<string, string>,
      body: typeof init.body === 'string' ? init.body : undefined,
    });

    const reply = queue.shift() ?? { status: 500 };

    return Promise.resolve({
      status: reply.status,
      ok: reply.status >= 200 && reply.status < 300,
      json: () =>
        reply.payload === undefined
          ? Promise.reject(new Error('no body'))
          : Promise.resolve(reply.payload),
    } as unknown as Response);
  }) as unknown as typeof fetch;
}

function reset(): void {
  calls.length = 0;
  forgetCsrfToken();
  onSessionLost(null);
}

const csrfReply: Reply = { status: 200, payload: { token: 'token-one' } };

async function main(): Promise<void> {
  console.log('apiClient checks\n');

  // -------------------------------------------------------------------
  // A read needs no token, and must not spend a round trip fetching one.
  // -------------------------------------------------------------------
  reset();
  stubFetch([{ status: 200, payload: { value: 1 } }]);
  const read = await apiGet<{ value: number }>('/api/product-groups');
  check('GET succeeds', read.ok && read.data.value === 1);
  check('GET fetches no CSRF token', calls.length === 1, `made ${calls.length} call(s)`);

  // -------------------------------------------------------------------
  // A write fetches the token once and reuses it afterwards.
  // -------------------------------------------------------------------
  reset();
  stubFetch([csrfReply, { status: 204 }, { status: 204 }]);
  await apiSend('POST', '/api/product-groups', { code: 'A' });
  await apiSend('POST', '/api/product-groups', { code: 'B' });

  const tokenFetches = calls.filter((call) => call.path === '/api/auth/csrf').length;
  check('the CSRF token is fetched once for two writes', tokenFetches === 1, `fetched ${tokenFetches} time(s)`);

  const writes = calls.filter((call) => call.method === 'POST');
  check(
    'every write carries the token header',
    writes.length === 2 && writes.every((call) => call.headers[CSRF_HEADER] === 'token-one'),
  );
  check('the body is sent as JSON', writes[0]?.body === JSON.stringify({ code: 'A' }));

  // -------------------------------------------------------------------
  // A stale token is refreshed once, then the answer stands.
  // -------------------------------------------------------------------
  reset();
  stubFetch([
    csrfReply,
    { status: 400, payload: { code: 'csrf_failed', title: 'nope' } },
    { status: 200, payload: { token: 'token-two' } },
    { status: 204 },
  ]);
  const retried = await apiSend('POST', '/api/product-groups', { code: 'C' });
  check('a stale CSRF token is refreshed and the write retried', retried.ok);

  const secondAttempt = calls.filter((call) => call.method === 'POST')[1];
  check('the retry uses the new token', secondAttempt?.headers[CSRF_HEADER] === 'token-two');

  reset();
  stubFetch([
    csrfReply,
    { status: 400, payload: { code: 'csrf_failed', title: 'nope' } },
    { status: 200, payload: { token: 'token-two' } },
    { status: 400, payload: { code: 'csrf_failed', title: 'nope' } },
  ]);
  const gaveUp = await apiSend('POST', '/api/product-groups', { code: 'D' });
  check(
    'it retries only once and then reports the failure',
    !gaveUp.ok && gaveUp.problem.code === 'csrf_failed',
  );

  // -------------------------------------------------------------------
  // A 401 on an ordinary call is a lost session; on the sign-in and session
  // endpoints it is simply the answer.
  // -------------------------------------------------------------------
  reset();
  let lost = 0;
  onSessionLost(() => {
    lost += 1;
  });
  stubFetch([{ status: 401, payload: { code: 'unauthorized', title: '' } }]);
  await apiGet('/api/product-groups');
  check('a 401 on an ordinary call reports a lost session', lost === 1, `reported ${lost} time(s)`);

  reset();
  let loginLost = 0;
  onSessionLost(() => {
    loginLost += 1;
  });
  stubFetch([{ status: 401, payload: { code: 'invalid_credentials', title: '' } }]);
  await apiGet('/api/auth/login');
  check('a 401 from sign-in does not report a lost session', loginLost === 0);

  reset();
  let meLost = 0;
  onSessionLost(() => {
    meLost += 1;
  });
  stubFetch([{ status: 401, payload: { code: 'unauthorized', title: '' } }]);
  await apiGet('/api/auth/me');
  check('a 401 from the session probe does not report a lost session', meLost === 0);

  // -------------------------------------------------------------------
  // Problem documents become values, including field errors.
  // -------------------------------------------------------------------
  reset();
  stubFetch([
    csrfReply,
    {
      status: 400,
      payload: {
        code: 'validation_failed',
        title: 'Girdiğiniz bilgilerde hata var.',
        errors: { code: ['Kod geçersiz.'], name: ['Ad zorunludur.'] },
      },
    },
  ]);
  const invalid = await apiSend('POST', '/api/product-groups', {});
  check('a validation failure is returned as a value', !invalid.ok);
  check(
    'field errors survive',
    !invalid.ok && firstFieldError(invalid.problem, 'code') === 'Kod geçersiz.',
  );

  reset();
  stubFetch([{ status: 500 }]);
  const broken = await apiGet('/api/product-groups');
  check(
    'a body-less failure still yields a code',
    !broken.ok && broken.problem.code === 'server_error',
  );

  reset();
  globalThis.fetch = (() => Promise.reject(new TypeError('offline'))) as unknown as typeof fetch;
  const offline = await apiGet('/api/product-groups');
  check('an unreachable server is reported, not thrown', !offline.ok && offline.problem.code === 'unreachable');

  // -------------------------------------------------------------------
  // Messages: a known code wins, an unknown one falls back to the title.
  // -------------------------------------------------------------------
  check(
    'a known code maps to Turkish',
    describeProblem({ status: 401, code: 'invalid_credentials', title: 'ignored' }) ===
      'E-posta veya parola hatalı.',
  );
  check(
    'an unknown code falls back to the server title',
    describeProblem({ status: 418, code: 'brand_new_code', title: 'Sunucu mesajı.' }) === 'Sunucu mesajı.',
  );
  check(
    'an unknown code with no title still says something',
    describeProblem({ status: 418, code: 'brand_new_code', title: '' }).length > 0,
  );

  if (failures > 0) {
    throw new Error(`${failures} apiClient check(s) failed.`);
  }
  console.log('\nAll apiClient checks passed.');
}

main().catch((error: unknown) => {
  console.error(error instanceof Error ? error.message : error);
  throw error;
});
