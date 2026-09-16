import { useState, type FormEvent } from 'react';
import { Navigate, useLocation } from 'react-router';
import type { ApiProblem } from '../../shared/apiClient.ts';
import { describeProblem } from '../../shared/messages.ts';
import { Alert, Field, Loading } from '../../shared/ui.tsx';
import { ThemeToggle } from '../../app/theme/ThemeToggle';
import { useAuth } from './useAuth.ts';

interface LocationState {
  from?: string;
}

export function LoginPage() {
  const { status, user, sessionExpired, signIn } = useAuth();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (status === 'loading') {
    return (
      <div className="co-centered">
        <Loading label="Oturum kontrol ediliyor…" />
      </div>
    );
  }

  if (status === 'authenticated' && user !== null) {
    const state = location.state as LocationState | null;

    return <Navigate to={state?.from ?? '/product-groups'} replace />;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setSubmitting(true);
    setProblem(await signIn(email, password));
    setSubmitting(false);
  }

  return (
    <div className="co-shell">
      <header className="co-header">
        <span className="co-brand">CommerceOps</span>
        <div className="co-session">
          <ThemeToggle />
        </div>
      </header>

      <div className="co-centered">
        <div className="co-card co-centered-card">
          <h1 className="co-page-title" style={{ marginBottom: 'var(--space-4)' }}>
            Giriş
          </h1>

          {sessionExpired && problem === null && (
            <div style={{ marginBottom: 'var(--space-4)' }}>
              <Alert kind="warning" title="Oturumunuz sonlandı.">
                <p style={{ margin: 0 }}>Devam etmek için tekrar giriş yapın.</p>
              </Alert>
            </div>
          )}

          {problem !== null && (
            <div style={{ marginBottom: 'var(--space-4)' }}>
              <Alert kind="error" title={describeProblem(problem)} />
            </div>
          )}

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
              autoComplete="username"
              required
              autoFocus
              disabled={submitting}
            />

            <Field
              label="Parola"
              type="password"
              value={password}
              onChange={setPassword}
              autoComplete="current-password"
              required
              disabled={submitting}
              // Always shown, never tied to an attempt: telling a visitor when
              // an account is locked would answer the question the uniform
              // error message deliberately refuses.
              hint="Çok sayıda hatalı denemeden sonra hesabınız geçici olarak kilitlenir."
            />

            <button type="submit" className="co-button co-button--primary co-button--block" disabled={submitting}>
              {submitting ? 'Giriş yapılıyor…' : 'Giriş yap'}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
