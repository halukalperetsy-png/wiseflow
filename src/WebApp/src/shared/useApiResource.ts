import { useCallback, useEffect, useState } from 'react';
import type { ApiProblem, ApiResult } from './apiClient.ts';

export type ResourceState = 'loading' | 'ready' | 'failed';

export interface Resource<T> {
  state: ResourceState;
  data: T | null;
  problem: ApiProblem | null;
  reload: () => void;
}

interface Snapshot<T> {
  /** Which loader produced this, so a different one reads as loading. */
  loader: unknown;
  state: ResourceState;
  data: T | null;
  problem: ApiProblem | null;
}

/**
 * Loads one thing from the API and reports the three states a screen has to
 * render: loading, ready, failed. The loader must be stable (useCallback), so
 * that reload() is the only thing that re-runs it.
 *
 * "Loading" is derived rather than assigned: a snapshot from a different loader
 * simply reads as loading, which avoids a second render pass.
 *
 * reload() deliberately keeps the current data on screen while it refetches. If
 * it blanked back to loading, the caller would unmount -- taking with it
 * anything held in the screen's own state, including a one-time password that
 * can never be shown again.
 */
export function useApiResource<T>(load: (signal: AbortSignal) => Promise<ApiResult<T>>): Resource<T> {
  const [attempt, setAttempt] = useState(0);

  const [snapshot, setSnapshot] = useState<Snapshot<T>>(() => ({
    loader: load,
    state: 'loading',
    data: null,
    problem: null,
  }));

  useEffect(() => {
    const controller = new AbortController();

    void load(controller.signal).then((result) => {
      if (controller.signal.aborted) {
        return;
      }

      setSnapshot(
        result.ok
          ? { loader: load, state: 'ready', data: result.data, problem: null }
          : { loader: load, state: 'failed', data: null, problem: result.problem },
      );
    });

    return () => {
      controller.abort();
    };
  }, [load, attempt]);

  const reload = useCallback(() => {
    setAttempt((value) => value + 1);
  }, []);

  const isCurrent = snapshot.loader === load;

  return isCurrent
    ? { state: snapshot.state, data: snapshot.data, problem: snapshot.problem, reload }
    : { state: 'loading', data: null, problem: null, reload };
}
