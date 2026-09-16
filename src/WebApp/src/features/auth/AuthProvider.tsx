import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import type { ApiProblem } from '../../shared/apiClient.ts';
import { onSessionLost } from '../../shared/apiClient.ts';
import { AuthContext, type AuthContextValue, type AuthStatus } from './authContext.ts';
import {
  fetchCurrentUser,
  signIn as requestSignIn,
  signOut as requestSignOut,
} from './authApi.ts';
import type { CurrentUser } from './types.ts';

/**
 * Holds the session for the whole app.
 *
 * The server revalidates the principal on every request, so this never has to
 * guess: whatever /api/auth/me last said is the truth, and a 401 anywhere else
 * means that truth has changed underneath us.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading');
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [sessionExpired, setSessionExpired] = useState(false);

  const load = useCallback(async (signal?: AbortSignal): Promise<void> => {
    const result = await fetchCurrentUser(signal);

    // The component may have gone away while the request was in flight.
    if (signal?.aborted === true) {
      return;
    }

    if (result.ok) {
      setUser(result.data);
      setStatus('authenticated');
      return;
    }

    setUser(null);
    setStatus('anonymous');
  }, []);

  useEffect(() => {
    const controller = new AbortController();

    // load() is async and only touches state after the session request
    // resolves; probing the server on mount is exactly the external-system
    // synchronisation this rule exists to allow.
    // oxlint-disable-next-line react/set-state-in-effect
    void load(controller.signal);

    return () => {
      controller.abort();
    };
  }, [load]);

  // One place learns that the session went away, instead of every screen
  // interpreting its own 401.
  useEffect(() => {
    onSessionLost(() => {
      setUser(null);
      setStatus('anonymous');
      setSessionExpired(true);
    });

    return () => {
      onSessionLost(null);
    };
  }, []);

  const signIn = useCallback(
    async (email: string, password: string): Promise<ApiProblem | null> => {
      const result = await requestSignIn(email, password);

      if (!result.ok) {
        return result.problem;
      }

      setSessionExpired(false);
      await load();

      return null;
    },
    [load],
  );

  const signOut = useCallback(async (): Promise<void> => {
    await requestSignOut();

    setUser(null);
    setStatus('anonymous');
    setSessionExpired(false);
  }, []);

  const refresh = useCallback(() => load(), [load]);

  const value = useMemo<AuthContextValue>(
    () => ({ status, user, sessionExpired, signIn, signOut, refresh }),
    [status, user, sessionExpired, signIn, signOut, refresh],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
