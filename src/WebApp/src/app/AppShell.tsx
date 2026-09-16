import { NavLink, Outlet } from 'react-router';
import { canManageUsers, canViewProductGroups } from '../features/auth/permissions.ts';
import { useAuth } from '../features/auth/useAuth.ts';
import { ThemeToggle } from './theme/ThemeToggle';

/**
 * The signed-in shell: brand, navigation, who is signed in, sign out, theme.
 *
 * Navigation only lists what this user may actually open. That is a usability
 * decision -- the API refuses the same call regardless of what is on screen.
 */
export function AppShell() {
  const { user, signOut } = useAuth();

  return (
    <div className="co-shell">
      <header className="co-header">
        <span className="co-brand">CommerceOps</span>

        <nav className="co-nav" aria-label="Ana menü">
          {canViewProductGroups(user) && (
            <NavLink className="co-nav-link" to="/product-groups">
              Ürün grupları
            </NavLink>
          )}
          {canManageUsers(user) && (
            <NavLink className="co-nav-link" to="/admin/users">
              Kullanıcılar
            </NavLink>
          )}
          {canManageUsers(user) && (
            <NavLink className="co-nav-link" to="/system">
              Sistem
            </NavLink>
          )}
        </nav>

        <div className="co-session">
          {user !== null && (
            <NavLink className="co-session-name co-link" to="/change-password">
              {user.displayName}
            </NavLink>
          )}
          <ThemeToggle />
          <button
            type="button"
            className="co-button"
            onClick={() => {
              void signOut();
            }}
          >
            Çıkış
          </button>
        </div>
      </header>

      <main className="co-main">
        <Outlet />
      </main>
    </div>
  );
}
