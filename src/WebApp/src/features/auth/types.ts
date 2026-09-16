/** What GET /api/auth/me returns about the signed-in user. */
export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  mustChangePassword: boolean;
  roles: string[];
  permissions: string[];
}
