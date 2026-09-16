import { createContext } from 'react';
import type { ApiProblem } from '../../shared/apiClient.ts';
import type { CurrentUser } from './types.ts';

/** loading covers the first session probe, before anything may be rendered. */
export type AuthStatus = 'loading' | 'anonymous' | 'authenticated';

export interface AuthContextValue {
  status: AuthStatus;
  user: CurrentUser | null;
  /**
   * True when a session that WAS signed in stopped being accepted -- the
   * account was deactivated, its password was reset elsewhere, or the cookie
   * expired. It is what separates "please sign in" from "you were signed out".
   */
  sessionExpired: boolean;
  /** Resolves to null on success, or the problem to show on the form. */
  signIn: (email: string, password: string) => Promise<ApiProblem | null>;
  signOut: () => Promise<void>;
  /** Re-reads the session, e.g. after the password change that clears the gate. */
  refresh: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
