import type { CurrentUser } from './types.ts';

/**
 * The permission names the backend enforces. Hiding a control here is a
 * usability decision, never a security one -- every one of these is checked
 * again on the server, and the API is what actually refuses.
 */
export const PERMISSIONS = {
  productGroupView: 'ProductGroup.View',
  userManagement: 'Admin.UserManagement',
  productGroupManagement: 'Admin.ProductGroupManagement',
} as const;

export type Permission = (typeof PERMISSIONS)[keyof typeof PERMISSIONS];

export function hasPermission(user: CurrentUser | null, permission: Permission): boolean {
  return user !== null && user.permissions.includes(permission);
}

export function canManageUsers(user: CurrentUser | null): boolean {
  return hasPermission(user, PERMISSIONS.userManagement);
}

export function canManageProductGroups(user: CurrentUser | null): boolean {
  return hasPermission(user, PERMISSIONS.productGroupManagement);
}

export function canViewProductGroups(user: CurrentUser | null): boolean {
  return hasPermission(user, PERMISSIONS.productGroupView);
}
