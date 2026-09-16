import { useState } from 'react';
import { Alert } from '../../shared/ui.tsx';

/**
 * The one moment a generated password is visible.
 *
 * It lives in component state and nowhere else -- not in the URL, not in
 * localStorage, not in the router's history state -- so a refresh loses it, which
 * is the intended behaviour. The server cannot show it again either.
 */
export function TemporaryPasswordNotice({ password }: { password: string }) {
  const [copied, setCopied] = useState(false);

  async function copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(password);
      setCopied(true);
    } catch {
      // Clipboard access can be refused; the value is on screen to read anyway.
      setCopied(false);
    }
  }

  return (
    <Alert kind="warning" title="Geçici parola bir kez gösteriliyor.">
      <p style={{ margin: 0 }}>
        Bu parolayı kullanıcıya uygulama dışından iletin. Sayfadan ayrıldığınızda bir daha
        görüntülenemez; gerekirse yeni bir parola oluşturmanız gerekir.
      </p>

      <p className="co-secret" style={{ margin: 'var(--space-2) 0 0' }}>
        <span className="co-hint" style={{ display: 'block' }}>
          Geçici parola
        </span>
        <code>{password}</code>
      </p>

      <div className="co-actions" style={{ marginTop: 'var(--space-2)' }}>
        <button
          type="button"
          className="co-button"
          onClick={() => {
            void copy();
          }}
        >
          Kopyala
        </button>
        {copied && (
          <span className="co-hint" role="status" aria-live="polite">
            <span aria-hidden="true">✓ </span>Panoya kopyalandı
          </span>
        )}
      </div>

      <p className="co-hint" style={{ margin: 'var(--space-2) 0 0' }}>
        Kullanıcı ilk girişinde bu parolayı değiştirmek zorundadır.
      </p>
    </Alert>
  );
}
