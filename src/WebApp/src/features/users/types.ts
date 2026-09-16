export interface UserListItem {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  mustChangePassword: boolean;
  roles: string[];
  createdAt: string;
}

export interface UserDetail {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  mustChangePassword: boolean;
  roles: string[];
  productGroupIds: string[];
  createdAt: string;
  updatedAt: string;
}

export interface RoleOption {
  id: string;
  code: string;
  displayName: string;
}

/**
 * Returned once, by the request that generated it. Never stored, never read
 * back, never written to browser storage.
 */
export interface IssuedPassword {
  id: string;
  temporaryPassword: string;
}
