import { apiGet, apiSend, forgetCsrfToken } from '../../shared/apiClient.ts';
import type { ApiResult } from '../../shared/apiClient.ts';
import type { CurrentUser } from './types.ts';

export function fetchCurrentUser(signal?: AbortSignal): Promise<ApiResult<CurrentUser>> {
  return apiGet<CurrentUser>('/api/auth/me', signal);
}

export async function signIn(email: string, password: string): Promise<ApiResult<void>> {
  const result = await apiSend<void>('POST', '/api/auth/login', { email, password });

  // The antiforgery token is bound to the caller's identity, so the one that
  // signed in is no longer the right one.
  if (result.ok) {
    forgetCsrfToken();
  }

  return result;
}

export async function signOut(): Promise<ApiResult<void>> {
  const result = await apiSend<void>('POST', '/api/auth/logout');
  forgetCsrfToken();

  return result;
}

export function changePassword(
  currentPassword: string,
  newPassword: string,
): Promise<ApiResult<void>> {
  return apiSend<void>('POST', '/api/auth/change-password', { currentPassword, newPassword });
}
