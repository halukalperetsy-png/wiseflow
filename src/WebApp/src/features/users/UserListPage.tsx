import { useCallback } from 'react';
import { Link } from 'react-router';
import { describeProblem } from '../../shared/messages.ts';
import { useApiResource } from '../../shared/useApiResource.ts';
import { ActiveBadge, Alert, EmptyState, Loading, PageHeader } from '../../shared/ui.tsx';
import { listUsers } from './userApi.ts';

export function UserListPage() {
  const load = useCallback((signal: AbortSignal) => listUsers(signal), []);
  const { state, data, problem, reload } = useApiResource(load);

  return (
    <div className="co-page">
      <PageHeader
        title="Kullanıcılar"
        subtitle="Hesap oluşturma, rol ve ürün grubu atama, aktif/pasif yapma."
        actions={
          <Link className="co-button co-button--primary" to="/admin/users/new">
            Yeni kullanıcı
          </Link>
        }
      />

      {state === 'loading' && <Loading />}

      {state === 'failed' && problem !== null && (
        <Alert kind="error" title={describeProblem(problem)}>
          <button type="button" className="co-button" onClick={reload}>
            Tekrar dene
          </button>
        </Alert>
      )}

      {state === 'ready' && data !== null && data.length === 0 && (
        <EmptyState title="Henüz kullanıcı yok." />
      )}

      {state === 'ready' && data !== null && data.length > 0 && (
        <div className="co-table-wrap">
          <table className="co-table">
            <caption className="co-hint" style={{ captionSide: 'bottom', padding: 'var(--space-2)' }}>
              {data.length} kullanıcı listeleniyor.
            </caption>
            <thead>
              <tr>
                <th scope="col">Ad</th>
                <th scope="col">E-posta</th>
                <th scope="col">Roller</th>
                <th scope="col">Durum</th>
              </tr>
            </thead>
            <tbody>
              {data.map((user) => (
                <tr key={user.id}>
                  <td>
                    <Link className="co-link" to={`/admin/users/${user.id}`}>
                      {user.displayName}
                    </Link>
                  </td>
                  <td className="co-muted">{user.email}</td>
                  <td>{user.roles.length > 0 ? user.roles.join(', ') : '—'}</td>
                  <td>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 'var(--space-2)' }}>
                      <ActiveBadge isActive={user.isActive} />
                      {user.mustChangePassword && (
                        <span className="co-badge co-badge--warning">
                          <span aria-hidden="true">!</span>
                          Parola değişikliği bekliyor
                        </span>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
