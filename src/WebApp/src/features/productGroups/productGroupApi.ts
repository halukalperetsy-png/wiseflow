import { apiGet, apiSend } from '../../shared/apiClient.ts';
import type { ApiResult } from '../../shared/apiClient.ts';
import type { ProductGroup } from './types.ts';

/**
 * The listing and the single record are both scoped server-side: an
 * administrator sees every group, anybody else sees only the active groups they
 * are assigned to, and a group outside that scope answers 404.
 */
export function listProductGroups(signal?: AbortSignal): Promise<ApiResult<ProductGroup[]>> {
  return apiGet<ProductGroup[]>('/api/product-groups', signal);
}

export function getProductGroup(id: string, signal?: AbortSignal): Promise<ApiResult<ProductGroup>> {
  return apiGet<ProductGroup>(`/api/product-groups/${id}`, signal);
}

export function createProductGroup(input: {
  code: string;
  name: string;
  description: string | null;
}): Promise<ApiResult<ProductGroup>> {
  return apiSend<ProductGroup>('POST', '/api/product-groups', input);
}

export function updateProductGroup(
  id: string,
  input: { name?: string; description?: string | null; isActive?: boolean },
): Promise<ApiResult<ProductGroup>> {
  return apiSend<ProductGroup>('PATCH', `/api/product-groups/${id}`, input);
}
