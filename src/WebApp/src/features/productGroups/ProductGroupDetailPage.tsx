import { useCallback } from 'react';
import { Link, useParams } from 'react-router';
import { describeProblem } from '../../shared/messages.ts';
import { useApiResource } from '../../shared/useApiResource.ts';
import { ActiveBadge, Alert, EmptyState, Loading, PageHeader } from '../../shared/ui.tsx';
import { canManageProductGroups } from '../auth/permissions.ts';
import { useAuth } from '../auth/useAuth.ts';
import { getProductGroup } from './productGroupApi.ts';

export function ProductGroupDetailPage() {
  const { id = '' } = useParams();
  const { user } = useAuth();
  const mayManage = canManageProductGroups(user);

  const load = useCallback((signal: AbortSignal) => getProductGroup(id, signal), [id]);
  const { state, data, problem } = useApiResource(load);

  return (
    <div className="co-page">
      <PageHeader
        title={data?.name ?? 'Ürün grubu'}
        subtitle={data?.code}
        actions={
          mayManage && data !== null ? (
            <Link className="co-button" to={`/product-groups/${data.id}/edit`}>
              Düzenle
            </Link>
          ) : undefined
        }
      />

      {state === 'loading' && <Loading />}

      {/* A group outside the caller's scope answers 404, so "not found" and
          "not yours" deliberately look the same here. */}
      {state === 'failed' && problem?.status === 404 && (
        <EmptyState
          title="Ürün grubu bulunamadı."
          hint="Bağlantı hatalı olabilir veya bu gruba erişim yetkiniz olmayabilir."
        />
      )}

      {state === 'failed' && problem !== null && problem.status !== 404 && (
        <Alert kind="error" title={describeProblem(problem)} />
      )}

      {state === 'ready' && data !== null && (
        <div className="co-card co-stack">
          <div>
            <div className="co-hint">Durum</div>
            <ActiveBadge isActive={data.isActive} />
          </div>
          <div>
            <div className="co-hint">Açıklama</div>
            <div>{data.description ?? '—'}</div>
          </div>
        </div>
      )}

      <p style={{ margin: 0 }}>
        <Link className="co-link" to="/product-groups">
          Listeye dön
        </Link>
      </p>
    </div>
  );
}
