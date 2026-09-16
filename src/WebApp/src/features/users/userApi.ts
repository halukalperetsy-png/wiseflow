import { apiGet, apiSend } from '../../shared/apiClient.ts';
import type { ApiResult } from '../../shared/apiClient.ts';
import type { IssuedPassword, RoleOption, UserDetail, UserListItem } from './types.ts';

export function listUsers(signal?: AbortSignal): Promise<ApiResult<UserListItem[]>> {
  return apiGet<UserListItem[]>('/api/admin/users', signal);
}

export function getUser(id: string, signal?: AbortSignal): Promise<ApiResult<UserDetail>> {
  return apiGet<UserDetail>(`/api/admin/users/${id}`, signal);
}

export function listRoles(signal?: AbortSignal): Promise<ApiResult<RoleOption[]>> {
  return apiGet<RoleOption[]>('/api/admin/roles', signal);
}

/** Answers with the one-time password; it is not stored anywhere. */
export function createUser(input: {
  email: string;
  displayName: string;
  roles: string[];
  productGroupIds: string[];
}): Promise<ApiResult<IssuedPassword>> {
  return apiSend<IssuedPassword>('POST', '/api/admin/users', input);
}

export function updateUser(
  id: string,
  input: { displayName?: string; isActive?: boolean },
): Promise<ApiResult<UserDetail>> {
  return apiSend<UserDetail>('PATCH', `/api/admin/users/${id}`, input);
}

export function setUserRoles(id: string, roles: string[]): Promise<ApiResult<void>> {
  return apiSend<void>('PUT', `/api/admin/users/${id}/roles`, { roles });
}

export function setUserProductGroups(
  id: string,
  productGroupIds: string[],
): Promise<ApiResult<void>> {
  return apiSend<void>('PUT', `/api/admin/users/${id}/product-groups`, { productGroupIds });
}

export function resetUserPassword(id: string): Promise<ApiResult<IssuedPassword>> {
  return apiSend<IssuedPassword>('POST', `/api/admin/users/${id}/reset-password`);
}
