import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import type { ApiProblem } from '../../shared/apiClient.ts';
import { describeProblem, firstFieldError } from '../../shared/messages.ts';
import { Alert, Field, PageHeader } from '../../shared/ui.tsx';
import { changePassword } from './authApi.ts';
import { useAuth } from './useAuth.ts';

const MINIMUM_LENGTH = 12;

/**
 * One screen for both cases: the forced change after an administrator issues a
 * temporary password, and a voluntary change later on.
 */
export function ChangePasswordPage() {
  const { user, refresh } = useAuth();
  const navigate = useNavigate();

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [repeated, setRepeated] = useState('');
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [mismatch, setMismatch] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);

  const forced = user?.mustChangePassword === true;

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setProblem(null);

    if (newPassword !== repeated) {
      setMismatch(true);
      return;
    }

    setMismatch(false);
    setSubmitting(true);

    const result = await changePassword(currentPassword, newPassword);

    setSubmitting(false);

    if (!result.ok) {
      setProblem(result.problem);
      return;
    }

    setDone(true);
    setCurrentPassword('');
    setNewPassword('');
    setRepeated('');

    // The gate is cleared server-side; re-read the session so the shell agrees.
    await refresh();

    if (forced) {
      void navigate('/product-groups', { replace: true });
    }
  }

  return (
    <div className="co-page">
      <PageHeader
        title="Parola değiştir"
        subtitle={
          forced
            ? 'Devam etmeden önce geçici parolanızı kendi parolanızla değiştirin.'
            : undefined
        }
      />

      {forced && (
        <Alert kind="warning" title="Parolanızı değiştirmeniz gerekiyor.">
          <p style={{ margin: 0 }}>
            Size verilen geçici parola yalnız ilk giriş içindir. Yeni bir parola belirleyene kadar
            diğer ekranlara erişemezsiniz.
          </p>
        </Alert>
      )}

      {done && !forced && <Alert kind="success" title="Parolanız değiştirildi." />}

      {problem !== null && problem.code !== 'validation_failed' && (
        <Alert kind="error" title={describeProblem(problem)} />
      )}

      <div className="co-card" style={{ maxWidth: '480px' }}>
        <form
          className="co-form"
          onSubmit={(event) => {
            void handleSubmit(event);
          }}
        >
          <Field
            label="Mevcut parola"
            type="password"
            value={currentPassword}
            onChange={setCurrentPassword}
            autoComplete="current-password"
            required
            autoFocus
            disabled={submitting}
            error={firstFieldError(problem, 'currentPassword')}
          />

          <Field
            label="Yeni parola"
            type="password"
            value={newPassword}
            onChange={setNewPassword}
            autoComplete="new-password"
            required
            disabled={submitting}
            hint={`En az ${String(MINIMUM_LENGTH)} karakter.`}
            error={firstFieldError(problem, 'newPassword')}
          />

          <Field
            label="Yeni parola (tekrar)"
            type="password"
            value={repeated}
            onChange={(value) => {
              setRepeated(value);
              setMismatch(false);
            }}
            autoComplete="new-password"
            required
            disabled={submitting}
            error={mismatch ? 'Parolalar eşleşmiyor.' : undefined}
          />

          <div className="co-actions">
            <button type="submit" className="co-button co-button--primary" disabled={submitting}>
              {submitting ? 'Kaydediliyor…' : 'Parolayı değiştir'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
