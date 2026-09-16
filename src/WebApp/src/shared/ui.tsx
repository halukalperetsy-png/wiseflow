import type { ReactNode } from 'react';
import { useId } from 'react';

/**
 * The handful of pieces every admin screen repeats. Status is always carried by
 * an icon AND words, never by colour alone -- the rule the Phase 0 health card
 * set.
 */

export function PageHeader({
  title,
  subtitle,
  actions,
}: {
  title: string;
  subtitle?: string;
  actions?: ReactNode;
}) {
  return (
    <div className="co-page-header">
      <div>
        <h1 className="co-page-title">{title}</h1>
        {subtitle !== undefined && <p className="co-page-subtitle">{subtitle}</p>}
      </div>
      {actions !== undefined && <div className="co-actions">{actions}</div>}
    </div>
  );
}

export function Field({
  label,
  value,
  onChange,
  type = 'text',
  hint,
  error,
  autoComplete,
  required = false,
  disabled = false,
  autoFocus = false,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: 'text' | 'email' | 'password';
  hint?: string;
  error?: string;
  autoComplete?: string;
  required?: boolean;
  disabled?: boolean;
  autoFocus?: boolean;
}) {
  const inputId = useId();
  const hintId = `${inputId}-hint`;
  const errorId = `${inputId}-error`;

  const describedBy = [hint !== undefined ? hintId : null, error !== undefined ? errorId : null]
    .filter((id) => id !== null)
    .join(' ');

  return (
    <div className="co-field">
      <label className="co-label" htmlFor={inputId}>
        {label}
        {required && <span aria-hidden="true"> *</span>}
      </label>
      <input
        id={inputId}
        className="co-input"
        type={type}
        value={value}
        onChange={(event) => {
          onChange(event.target.value);
        }}
        autoComplete={autoComplete}
        required={required}
        disabled={disabled}
        // Single-purpose screens put the caret in the field that matters.
        autoFocus={autoFocus}
        aria-invalid={error !== undefined}
        aria-describedby={describedBy === '' ? undefined : describedBy}
      />
      {hint !== undefined && (
        <span className="co-hint" id={hintId}>
          {hint}
        </span>
      )}
      {error !== undefined && (
        <span className="co-field-error" id={errorId}>
          {error}
        </span>
      )}
    </div>
  );
}

const ALERT_ICONS = {
  error: '×',
  success: '✓',
  warning: '!',
  info: 'i',
} as const;

export function Alert({
  kind,
  title,
  children,
}: {
  kind: keyof typeof ALERT_ICONS;
  title: string;
  children?: ReactNode;
}) {
  return (
    <div
      className={`co-alert co-alert--${kind}`}
      role={kind === 'error' ? 'alert' : 'status'}
      aria-live={kind === 'error' ? 'assertive' : 'polite'}
    >
      <span className="co-alert-icon" aria-hidden="true">
        {ALERT_ICONS[kind]}
      </span>
      <div className="co-alert-body">
        <span className="co-alert-title">{title}</span>
        {children}
      </div>
    </div>
  );
}

export function Loading({ label = 'Yükleniyor…' }: { label?: string }) {
  return (
    <p className="co-muted" role="status" aria-live="polite">
      <span aria-hidden="true">◷ </span>
      {label}
    </p>
  );
}

export function EmptyState({ title, hint }: { title: string; hint?: string }) {
  return (
    <div className="co-empty">
      <p style={{ margin: 0, fontWeight: 600 }}>{title}</p>
      {hint !== undefined && <p style={{ margin: 'var(--space-2) 0 0' }}>{hint}</p>}
    </div>
  );
}

export function ActiveBadge({ isActive }: { isActive: boolean }) {
  return (
    <span className={`co-badge ${isActive ? 'co-badge--on' : 'co-badge--off'}`}>
      <span aria-hidden="true">{isActive ? '✓' : '○'}</span>
      {isActive ? 'Aktif' : 'Pasif'}
    </span>
  );
}
