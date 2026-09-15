/*
 * Regression checks for the two defects found in probeHealth.
 *
 * Run with:  npm run check:health
 *
 * Deliberately dependency-free: Node 24 executes TypeScript directly, so this
 * needs no test runner. It stubs fetch and drives probeHealth through the two
 * paths that were wrong, plus one healthy-path sanity check. Throwing on
 * failure is what makes the exit code non-zero.
 */

import { probeHealth } from './healthApi.ts';

let failures = 0;

function check(name: string, passed: boolean, detail = ''): void {
  if (passed) {
    console.log(`  PASS  ${name}`);
    return;
  }
  failures += 1;
  console.error(`  FAIL  ${name}${detail ? ` -- ${detail}` : ''}`);
}

/** Replaces global fetch with a handler returning a minimal Response stand-in. */
function stubFetch(status: number, json: () => Promise<unknown>): void {
  globalThis.fetch = (() =>
    Promise.resolve({ status, json } as unknown as Response)) as unknown as typeof fetch;
}

function abortedWith(reason: DOMException): AbortSignal {
  const controller = new AbortController();
  controller.abort(reason);
  return controller.signal;
}

const timeoutReason = () => new DOMException('The operation timed out.', 'TimeoutError');
const abortReason = () => new DOMException('This operation was aborted.', 'AbortError');

const liveSignal = () => new AbortController().signal;

/**
 * Runs probeHealth and reports an unexpected throw as the string 'threw'
 * instead of aborting the whole run, so a regression shows up as a FAIL line
 * rather than a crash.
 */
async function probeKind(signal: AbortSignal): Promise<string> {
  try {
    return (await probeHealth(signal)).kind;
  } catch {
    return 'threw';
  }
}

async function main(): Promise<void> {
  console.log('healthApi regression checks\n');

  // ---------------------------------------------------------------------
  // Defect 1: cancellation that lands while the body is being read.
  // The body read rejects with a plain AbortError even when the cause was the
  // timeout, so the cause has to come from the signal. Reading only the error
  // name classified this as an unmount, probeHealth rethrew, the caller
  // swallowed it, and the card stayed on its previous (stale) result.
  // ---------------------------------------------------------------------
  stubFetch(200, () => Promise.reject(abortReason()));
  const timedOut = await probeKind(abortedWith(timeoutReason()));
  check('timeout while reading the body -> timeout', timedOut === 'timeout', `got "${timedOut}"`);

  // Only a genuine unmount may be swallowed by the caller.
  stubFetch(200, () => Promise.reject(abortReason()));
  let rethrown = false;
  try {
    await probeHealth(abortedWith(abortReason()));
  } catch {
    rethrown = true;
  }
  check('unmount while reading the body -> rethrown for the caller to drop', rethrown);

  // Same distinction on the fetch call itself, not just the body read.
  globalThis.fetch = (() => Promise.reject(abortReason())) as unknown as typeof fetch;
  const timedOutOnFetch = await probeKind(abortedWith(timeoutReason()));
  check('timeout during the request -> timeout', timedOutOnFetch === 'timeout', `got "${timedOutOnFetch}"`);

  // ---------------------------------------------------------------------
  // Defect 2: a nested check status outside the known set.
  // The top-level status agrees with the HTTP code, so only the per-check
  // validation can reject this.
  // ---------------------------------------------------------------------
  stubFetch(200, () =>
    Promise.resolve({
      status: 'Healthy',
      durationMs: 1,
      checks: [{ name: 'database', status: 'unknown', durationMs: 1 }],
    }),
  );
  const unknownNested = await probeKind(liveSignal());
  check('unknown nested check status -> invalid', unknownNested === 'invalid', `got "${unknownNested}"`);

  // A bad status among several good ones must still be rejected.
  stubFetch(503, () =>
    Promise.resolve({
      status: 'Unhealthy',
      durationMs: 1,
      checks: [
        { name: 'database', status: 'Unhealthy', durationMs: 1 },
        { name: 'storage', status: 'Broken', durationMs: 1 },
      ],
    }),
  );
  const oneBadCheck = await probeKind(liveSignal());
  check('one invalid status among valid ones -> invalid', oneBadCheck === 'invalid', `got "${oneBadCheck}"`);

  // ---------------------------------------------------------------------
  // Sanity: the good paths still classify correctly.
  // ---------------------------------------------------------------------
  stubFetch(200, () =>
    Promise.resolve({
      status: 'Healthy',
      durationMs: 1,
      checks: [{ name: 'database', status: 'Healthy', durationMs: 1 }],
    }),
  );
  const healthy = await probeKind(liveSignal());
  check('valid healthy payload -> healthy', healthy === 'healthy', `got "${healthy}"`);

  stubFetch(503, () =>
    Promise.resolve({
      status: 'Unhealthy',
      durationMs: 1,
      checks: [{ name: 'database', status: 'Unhealthy', durationMs: 1 }],
    }),
  );
  const unhealthy = await probeKind(liveSignal());
  check('valid 503 payload -> unhealthy (not unreachable)', unhealthy === 'unhealthy', `got "${unhealthy}"`);

  if (failures > 0) {
    throw new Error(`${failures} healthApi regression check(s) failed.`);
  }
  console.log('\nAll healthApi regression checks passed.');
}

main().catch((error: unknown) => {
  console.error(error instanceof Error ? error.message : error);
  throw error;
});
