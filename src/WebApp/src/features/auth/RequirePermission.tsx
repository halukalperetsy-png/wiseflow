import type { ReactNode } from 'react';
import { Navigate } from 'react-router';
import { hasPermission, type Permission } from './permissions.ts';
import { useAuth } from './useAuth.ts';

/**
 * Keeps a screen the user has no permission for from rendering. This is
 * usability, not security: the API refuses the same call independently.
 */
export function RequirePermission({
  permission,
  children,
}: {
  permission: Permission;
  children: ReactNode;
}) {
  const { user } = useAuth();

  if (!hasPermission(user, permission)) {
    return <Navigate to="/forbidden" replace />;
  }

  return children;
}
