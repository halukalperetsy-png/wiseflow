import { useCallback, useState, type FormEvent, type ReactNode } from 'react';
import { Link, useParams } from 'react-router';
import type { ApiProblem, ApiResult } from '../../shared/apiClient.ts';
import { describeProblem, firstFieldError } from '../../shared/messages.ts';
import { useApiResource } from '../../shared/useApiResource.ts';
import { ActiveBadge, Alert, EmptyState, Field, Loading, PageHeader } from '../../shared/ui.tsx';
import { useAuth } from '../auth/useAuth.ts';
import { listProductGroups } from '../productGroups/productGroupApi.ts';
import type { ProductGroup } from '../productGroups/types.ts';
import { TemporaryPasswordNotice } from './TemporaryPasswordNotice.tsx';
import {
  createUser,
  getUser,
  listRoles,
  resetUserPassword,
  setUserProductGroups,
  setUserRoles,
  updateUser,
} from './userApi.ts';
import type { RoleOption, UserDetail } from './types.ts';

interface FormResources {
  user: UserDetail | null;
  roles: RoleOption[];
  groups: ProductGroup[];
}

/**
 * Creating a user and editing one.
 *
 * Editing is split into the three things the API treats separately -- profile,
 * roles, product group access -- so that a refused change (the last
 * administrator rule, say) cannot half-apply the others.
 */
export function UserFormPage({ mode }: { mode: 'create' | 'edit' }) {
  const { id = '' } = useParams();

  const load = useCallback(
    async (signal: AbortSignal): Promise<ApiResult<FormResources>> => {
      const [roles, groups, user] = await Promise.all([
        listRoles(signal),
        listProductGroups(signal),
        mode === 'edit'
          ? getUser(id, signal)
          : Promise.resolve<ApiResult<UserDetail | null>>({ ok: true, data: null }),
      ]);

      if (!roles.ok) {
        return roles;
      }
      if (!groups.ok) {
        return groups;
      }
      if (!user.ok) {
        return user;
      }

      return { ok: true, data: { roles: roles.data, groups: groups.data, user: user.data } };
    },
    [mode, id],
  );

  const { state, data, problem, reload } = useApiResource(load);

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
        <PageHeader title="Kullanıcı" />
        {problem?.status === 404 ? (
          <EmptyState title="Kullanıcı bulunamadı." />
        ) : (
          <Alert
            kind="error"
            title={problem === null ? 'Kayıt yüklenemedi.' : describeProblem(problem)}
          />
        )}
        <p style={{ margin: 0 }}>
          <Link className="co-link" to="/admin/users">
            Listeye dön
          </Link>
        </p>
      </div>
    );
  }

  if (data.user === null) {
    return <CreateUserForm roles={data.roles} groups={data.groups} />;
  }

  return (
    <EditUserForm
      key={data.user.id}
      user={data.user}
      roles={data.roles}
      groups={data.groups}
      onSaved={reload}
    />
  );
}

function CreateUserForm({ roles, groups }: { roles: RoleOption[]; groups: ProductGroup[] }) {
  const [email, setEmail] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
  const [selectedGroups, setSelectedGroups] = useState<string[]>([]);
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [busy, setBusy] = useState(false);
  const [issued, setIssued] = useState<{ id: string; password: string } | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setProblem(null);
    setBusy(true);

    const result = await createUser({
      email: email.trim(),
      displayName: displayName.trim(),
      roles: selectedRoles,
      productGroupIds: selectedGroups,
    });

    setBusy(false);

    if (!result.ok) {
      setProblem(result.problem);
      return;
    }

    // Component state only. A refresh loses it, which is the intent: the server
    // cannot show it again either.
    setIssued({ id: result.data.id, password: result.data.temporaryPassword });
  }

  if (issued !== null) {
    return (
      <div className="co-page">
        <PageHeader title="Kullanıcı oluşturuldu" subtitle={email} />
        <TemporaryPasswordNotice password={issued.password} />
        <div className="co-actions">
          <Link className="co-button co-button--primary" to={`/admin/users/${issued.id}`}>
            Kullanıcıyı düzenle
          </Link>
          <Link className="co-button" to="/admin/users">
            Kullanıcı listesine dön
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="co-page">
      <PageHeader title="Yeni kullanıcı" subtitle="Kaydettiğinizde bir geçici parola üretilir." />

      {problem !== null && problem.code !== 'validation_failed' && (
        <Alert kind="error" title={describeProblem(problem)} />
      )}

      <div className="co-card" style={{ maxWidth: '560px' }}>
        <form
          className="co-form"
          onSubmit={(event) => {
            void handleSubmit(event);
          }}
        >
          <Field
            label="E-posta"
            type="email"
            value={email}
            onChange={setEmail}
            required
            autoFocus
            disabled={busy}
            hint="Kullanıcı bu adresle giriş yapar."
            error={firstFieldError(problem, 'email')}
          />

          <Field
            label="Ad"
            value={displayName}
            onChange={setDisplayName}
            required
            disabled={busy}
            error={firstFieldError(problem, 'displayName')}
          />

          <CheckboxFieldset legend="Roller" error={firstFieldError(problem, 'roles')}>
            <RoleChecklist
              roles={roles}
              selected={selectedRoles}
              disabled={busy}
              onToggle={(code) => {
                setSelectedRoles(toggle(selectedRoles, code));
              }}
            />
          </CheckboxFieldset>

          <CheckboxFieldset
            legend="Ürün grubu erişimi"
            error={firstFieldError(problem, 'productGroupIds')}
          >
            <GroupChecklist
              groups={groups}
              selected={selectedGroups}
              disabled={busy}
              onToggle={(groupId) => {
                setSelectedGroups(toggle(selectedGroups, groupId));
              }}
            />
          </CheckboxFieldset>

          <div className="co-actions">
            <button type="submit" className="co-button co-button--primary" disabled={busy}>
              {busy ? 'Oluşturuluyor…' : 'Kullanıcıyı oluştur'}
            </button>
            <Link className="co-button" to="/admin/users">
              Vazgeç
            </Link>
          </div>
        </form>
      </div>
    </div>
  );
}

function EditUserForm({
  user,
  roles,
  groups,
  onSaved,
}: {
  user: UserDetail;
  roles: RoleOption[];
  groups: ProductGroup[];
  onSaved: () => void;
}) {
  const { user: signedInUser } = useAuth();
  const editingSelf = signedInUser?.id === user.id;

  const [displayName, setDisplayName] = useState(user.displayName);
  const [isActive, setIsActive] = useState(user.isActive);
  const [selectedRoles, setSelectedRoles] = useState<string[]>(user.roles);
  const [selectedGroups, setSelectedGroups] = useState<string[]>(user.productGroupIds);

  const [profileProblem, setProfileProblem] = useState<ApiProblem | null>(null);
  const [rolesProblem, setRolesProblem] = useState<ApiProblem | null>(null);
  const [groupsProblem, setGroupsProblem] = useState<ApiProblem | null>(null);
  const [saved, setSaved] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [issuedPassword, setIssuedPassword] = useState<string | null>(null);

  async function run(
    action: () => Promise<ApiResult<unknown>>,
    onFailure: (problem: ApiProblem) => void,
    success: string | null,
  ): Promise<void> {
    setProfileProblem(null);
    setRolesProblem(null);
    setGroupsProblem(null);
    setSaved(null);
    setBusy(true);

    const result = await action();

    setBusy(false);

    if (!result.ok) {
      onFailure(result.problem);
      return;
    }

    setSaved(success);
    onSaved();
  }

  return (
    <div className="co-page">
      <PageHeader
        title={user.displayName}
        subtitle={user.email}
        actions={<ActiveBadge isActive={user.isActive} />}
      />

      {saved !== null && <Alert kind="success" title={saved} />}
      {issuedPassword !== null && <TemporaryPasswordNotice password={issuedPassword} />}

      {editingSelf && (
        <Alert kind="info" title="Bu sizin hesabınız.">
          <p style={{ margin: 0 }}>
            Kendi rollerinizi ve ürün grubu erişiminizi değiştiremez, hesabınızı
            pasifleştiremezsiniz. Bu değişiklikleri başka bir yönetici yapabilir.
          </p>
        </Alert>
      )}

      <section className="co-card">
        <h2 className="co-page-title" style={{ fontSize: '16px', marginBottom: 'var(--space-4)' }}>
          Hesap
        </h2>

        {profileProblem !== null && profileProblem.code !== 'validation_failed' && (
          <div style={{ marginBottom: 'var(--space-4)' }}>
            <Alert kind="error" title={describeProblem(profileProblem)} />
          </div>
        )}

        <form
          className="co-form"
          onSubmit={(event) => {
            event.preventDefault();
            void run(
              () => updateUser(user.id, { displayName: displayName.trim(), isActive }),
              setProfileProblem,
              'Hesap bilgileri kaydedildi.',
            );
          }}
        >
          <Field
            label="Ad"
            value={displayName}
            onChange={setDisplayName}
            required
            disabled={busy}
            error={firstFieldError(profileProblem, 'displayName')}
          />

          <div className="co-check">
            <input
              id="user-active"
              type="checkbox"
              checked={isActive}
              disabled={busy || editingSelf}
              onChange={(event) => {
                setIsActive(event.target.checked);
              }}
            />
            <label htmlFor="user-active">
              Aktif
              <span className="co-hint" style={{ display: 'block' }}>
                Pasif kullanıcı giriş yapamaz ve açık oturumu bir sonraki istekte sonlanır.
              </span>
            </label>
          </div>

          <div className="co-actions">
            <button type="submit" className="co-button co-button--primary" disabled={busy}>
              Hesabı kaydet
            </button>
            <button
              type="button"
              className="co-button co-button--danger"
              disabled={busy}
              onClick={() => {
                void run(
                  async () => {
                    const result = await resetUserPassword(user.id);

                    if (result.ok) {
                      setIssuedPassword(result.data.temporaryPassword);
                    }

                    return result;
                  },
                  setProfileProblem,
                  null,
                );
              }}
            >
              Parolayı sıfırla
            </button>
          </div>
        </form>
      </section>

      <section className="co-card">
        <h2 className="co-page-title" style={{ fontSize: '16px', marginBottom: 'var(--space-4)' }}>
          Roller
        </h2>

        {rolesProblem !== null && (
          <div style={{ marginBottom: 'var(--space-4)' }}>
            <Alert kind="error" title={describeProblem(rolesProblem)} />
          </div>
        )}

        <form
          className="co-form"
          onSubmit={(event) => {
            event.preventDefault();
            void run(
              () => setUserRoles(user.id, selectedRoles),
              setRolesProblem,
              'Roller kaydedildi.',
            );
          }}
        >
          <RoleChecklist
            roles={roles}
            selected={selectedRoles}
            disabled={busy || editingSelf}
            onToggle={(code) => {
              setSelectedRoles(toggle(selectedRoles, code));
            }}
          />
          <div className="co-actions">
            <button
              type="submit"
              className="co-button co-button--primary"
              disabled={busy || editingSelf}
            >
              Rolleri kaydet
            </button>
          </div>
        </form>
      </section>

      <section className="co-card">
        <h2 className="co-page-title" style={{ fontSize: '16px', marginBottom: 'var(--space-4)' }}>
          Ürün grubu erişimi
        </h2>

        {groupsProblem !== null && (
          <div style={{ marginBottom: 'var(--space-4)' }}>
            <Alert kind="error" title={describeProblem(groupsProblem)} />
          </div>
        )}

        <form
          className="co-form"
          onSubmit={(event) => {
            event.preventDefault();
            void run(
              () => setUserProductGroups(user.id, selectedGroups),
              setGroupsProblem,
              'Ürün grubu erişimi kaydedildi.',
            );
          }}
        >
          <GroupChecklist
            groups={groups}
            selected={selectedGroups}
            disabled={busy || editingSelf}
            onToggle={(groupId) => {
              setSelectedGroups(toggle(selectedGroups, groupId));
            }}
          />
          <div className="co-actions">
            <button
              type="submit"
              className="co-button co-button--primary"
              disabled={busy || editingSelf}
            >
              Erişimi kaydet
            </button>
          </div>
        </form>
      </section>

      <p style={{ margin: 0 }}>
        <Link className="co-link" to="/admin/users">
          Listeye dön
        </Link>
      </p>
    </div>
  );
}

function CheckboxFieldset({
  legend,
  error,
  children,
}: {
  legend: string;
  error?: string;
  children: ReactNode;
}) {
  return (
    <fieldset className="co-fieldset">
      <legend className="co-legend">{legend}</legend>
      {children}
      {error !== undefined && (
        <p className="co-field-error" style={{ margin: 'var(--space-2) 0 0' }}>
          {error}
        </p>
      )}
    </fieldset>
  );
}

function RoleChecklist({
  roles,
  selected,
  disabled,
  onToggle,
}: {
  roles: RoleOption[];
  selected: string[];
  disabled: boolean;
  onToggle: (code: string) => void;
}) {
  return (
    <div className="co-checklist">
      {roles.map((role) => (
        <div className="co-check" key={role.id}>
          <input
            id={`role-${role.id}`}
            type="checkbox"
            checked={selected.includes(role.code)}
            disabled={disabled}
            onChange={() => {
              onToggle(role.code);
            }}
          />
          <label htmlFor={`role-${role.id}`}>{role.displayName}</label>
        </div>
      ))}
    </div>
  );
}

function GroupChecklist({
  groups,
  selected,
  disabled,
  onToggle,
}: {
  groups: ProductGroup[];
  selected: string[];
  disabled: boolean;
  onToggle: (id: string) => void;
}) {
  if (groups.length === 0) {
    return (
      <p className="co-muted" style={{ margin: 0 }}>
        Henüz ürün grubu yok.{' '}
        <Link className="co-link" to="/product-groups/new">
          Bir ürün grubu oluşturun.
        </Link>
      </p>
    );
  }

  return (
    <div className="co-checklist">
      {groups.map((group) => (
        <div className="co-check" key={group.id}>
          <input
            id={`group-${group.id}`}
            type="checkbox"
            checked={selected.includes(group.id)}
            disabled={disabled}
            onChange={() => {
              onToggle(group.id);
            }}
          />
          <label htmlFor={`group-${group.id}`}>
            {group.code} — {group.name}
            {!group.isActive && (
              <span className="co-hint" style={{ display: 'block' }}>
                Bu grup pasif.
              </span>
            )}
          </label>
        </div>
      ))}
    </div>
  );
}

function toggle(list: string[], value: string): string[] {
  return list.includes(value) ? list.filter((item) => item !== value) : [...list, value];
}
