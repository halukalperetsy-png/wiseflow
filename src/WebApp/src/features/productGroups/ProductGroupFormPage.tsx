import { useCallback, useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import type { ApiProblem, ApiResult } from '../../shared/apiClient.ts';
import { describeProblem, firstFieldError } from '../../shared/messages.ts';
import { useApiResource } from '../../shared/useApiResource.ts';
import { Alert, EmptyState, Field, Loading, PageHeader } from '../../shared/ui.tsx';
import { createProductGroup, getProductGroup, updateProductGroup } from './productGroupApi.ts';
import type { ProductGroup } from './types.ts';

/**
 * One form for creating and for editing. The code is fixed once the group
 * exists -- other records will refer to it from Phase 2 onwards.
 *
 * The loading and the editing live in two components on purpose: the inner one
 * only mounts when there is something to edit, so its fields start from the
 * loaded values instead of being copied in by an effect afterwards.
 */
export function ProductGroupFormPage({ mode }: { mode: 'create' | 'edit' }) {
  const { id = '' } = useParams();

  // Create mode has nothing to fetch, so it resolves immediately rather than
  // asking the API for a group with no id.
  const load = useCallback(
    (signal: AbortSignal): Promise<ApiResult<ProductGroup | null>> =>
      mode === 'create'
        ? Promise.resolve<ApiResult<ProductGroup | null>>({ ok: true, data: null })
        : getProductGroup(id, signal),
    [mode, id],
  );

  const { state, data, problem } = useApiResource(load);

  if (mode === 'create') {
    return <ProductGroupForm mode="create" group={null} />;
  }

  if (state === 'loading') {
    return (
      <div className="co-page">
        <Loading />
      </div>
    );
  }

  if (state === 'failed' || data === null) {
    return (
      <div className="co-page">
        <PageHeader title="Ürün grubu" />
        {problem?.status === 404 ? (
          <EmptyState title="Ürün grubu bulunamadı." />
        ) : (
          <Alert
            kind="error"
            title={problem === null ? 'Kayıt yüklenemedi.' : describeProblem(problem)}
          />
        )}
        <p style={{ margin: 0 }}>
          <Link className="co-link" to="/product-groups">
            Listeye dön
          </Link>
        </p>
      </div>
    );
  }

  return <ProductGroupForm key={data.id} mode="edit" group={data} />;
}

function ProductGroupForm({
  mode,
  group,
}: {
  mode: 'create' | 'edit';
  group: ProductGroup | null;
}) {
  const navigate = useNavigate();

  const [code, setCode] = useState(group?.code ?? '');
  const [name, setName] = useState(group?.name ?? '');
  const [description, setDescription] = useState(group?.description ?? '');
  const [isActive, setIsActive] = useState(group?.isActive ?? true);
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setProblem(null);
    setSubmitting(true);

    const trimmed = description.trim();
    const normalizedDescription = trimmed === '' ? null : trimmed;

    const result =
      group === null
        ? await createProductGroup({
            code: code.trim(),
            name: name.trim(),
            description: normalizedDescription,
          })
        : await updateProductGroup(group.id, {
            name: name.trim(),
            description: normalizedDescription,
            isActive,
          });

    setSubmitting(false);

    if (!result.ok) {
      setProblem(result.problem);
      return;
    }

    void navigate(`/product-groups/${result.data.id}`, { replace: true });
  }

  return (
    <div className="co-page">
      <PageHeader title={mode === 'create' ? 'Yeni ürün grubu' : 'Ürün grubunu düzenle'} />

      {problem !== null && problem.code !== 'validation_failed' && (
        <Alert kind="error" title={describeProblem(problem)} />
      )}

      <div className="co-card" style={{ maxWidth: '520px' }}>
        <form
          className="co-form"
          onSubmit={(event) => {
            void handleSubmit(event);
          }}
        >
          <Field
            label="Kod"
            value={code}
            onChange={(value) => {
              setCode(value.toUpperCase());
            }}
            required
            autoFocus={mode === 'create'}
            disabled={mode === 'edit' || submitting}
            hint={
              mode === 'edit'
                ? 'Kod oluşturulduktan sonra değiştirilemez.'
                : 'Büyük harf, rakam ve tire. Örnek: ORNAMENTS'
            }
            error={firstFieldError(problem, 'code')}
          />

          <Field
            label="Ad"
            value={name}
            onChange={setName}
            required
            autoFocus={mode === 'edit'}
            disabled={submitting}
            error={firstFieldError(problem, 'name')}
          />

          <Field
            label="Açıklama"
            value={description}
            onChange={setDescription}
            disabled={submitting}
            hint="İsteğe bağlı."
            error={firstFieldError(problem, 'description')}
          />

          {mode === 'edit' && (
            <div className="co-check">
              <input
                id="product-group-active"
                type="checkbox"
                checked={isActive}
                disabled={submitting}
                onChange={(event) => {
                  setIsActive(event.target.checked);
                }}
              />
              <label htmlFor="product-group-active">
                Aktif
                <span className="co-hint" style={{ display: 'block' }}>
                  Pasif gruplar kullanıcı listelerinde görünmez. Kayıt silinmez.
                </span>
              </label>
            </div>
          )}

          <div className="co-actions">
            <button type="submit" className="co-button co-button--primary" disabled={submitting}>
              {submitting ? 'Kaydediliyor…' : 'Kaydet'}
            </button>
            <Link className="co-button" to="/product-groups">
              Vazgeç
            </Link>
          </div>
        </form>
      </div>
    </div>
  );
}
