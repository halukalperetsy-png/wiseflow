import { Navigate, Outlet, useLocation } from 'react-router';
import { Loading } from '../../shared/ui.tsx';
import { useAuth } from './useAuth.ts';

/**
 * Gate for every screen behind the sign-in.
 *
 * It also holds a user who still owes a password change to that one screen, the
 * same way the API does -- otherwise the UI would offer links that all answer
 * 403.
 */
export function RequireAuth() {
  const { status, user } = useAuth();
  const location = useLocation();

  if (status === 'loading') {
    return (
      <div className="co-centered">
        <Loading label="Oturum kontrol ediliyor…" />
      </div>
    );
  }

  if (status === 'anonymous' || user === null) {
    // Remember where they were headed so sign-in can send them back.
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  }

  if (user.mustChangePassword && location.pathname !== '/change-password') {
    return <Navigate to="/change-password" replace />;
  }

  return <Outlet />;
}
