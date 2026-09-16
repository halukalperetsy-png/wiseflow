import { useCallback } from 'react';
import { Link } from 'react-router';
import { describeProblem } from '../../shared/messages.ts';
import { useApiResource } from '../../shared/useApiResource.ts';
import { ActiveBadge, Alert, EmptyState, Loading, PageHeader } from '../../shared/ui.tsx';
import { canManageProductGroups } from '../auth/permissions.ts';
import { useAuth } from '../auth/useAuth.ts';
import { listProductGroups } from './productGroupApi.ts';

/**
 * The starting screen. For an ordinary user it is the list of groups they may
 * work in; an administrator sees every group, retired ones included, plus the
 * controls to change them.
 */
export function ProductGroupListPage() {
  const { user } = useAuth();
  const mayManage = canManageProductGroups(user);

  const load = useCallback((signal: AbortSignal) => listProductGroups(signal), []);
  const { state, data, problem, reload } = useApiResource(load);

  return (
    <div className="co-page">
      <PageHeader
        title="Ürün grupları"
        subtitle={
          mayManage
            ? 'Tüm ürün grupları. Pasif gruplar da listelenir.'
            : 'Erişim yetkiniz olan aktif ürün grupları.'
        }
        actions={
          mayManage ? (
            <Link className="co-button co-button--primary" to="/product-groups/new">
              Yeni ürün grubu
            </Link>
          ) : undefined
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
        <EmptyState
          title="Henüz ürün grubu yok."
          hint={
            mayManage
              ? 'İlk ürün grubunu oluşturmak için “Yeni ürün grubu” düğmesini kullanın.'
              : 'Size henüz bir ürün grubu atanmamış. Bir yöneticiyle görüşün.'
          }
        />
      )}

      {state === 'ready' && data !== null && data.length > 0 && (
        <div className="co-table-wrap">
          <table className="co-table">
            <caption className="co-hint" style={{ captionSide: 'bottom', padding: 'var(--space-2)' }}>
              {data.length} ürün grubu listeleniyor.
            </caption>
            <thead>
              <tr>
                <th scope="col">Kod</th>
                <th scope="col">Ad</th>
                <th scope="col">Açıklama</th>
                <th scope="col">Durum</th>
              </tr>
            </thead>
            <tbody>
              {data.map((group) => (
                <tr key={group.id}>
                  <td>
                    <Link className="co-link" to={`/product-groups/${group.id}`}>
                      {group.code}
                    </Link>
                  </td>
                  <td>{group.name}</td>
                  <td className="co-muted">{group.description ?? '—'}</td>
                  <td>
                    <ActiveBadge isActive={group.isActive} />
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
