import type { ReactNode } from 'react';
import { ThemeToggle } from './theme/ThemeToggle';

/**
 * Phase 0 shell: header plus content. No sidebar yet -- there is no second
 * page to navigate to. It arrives with the real menu in a later phase.
 */
export function AppShell({ children }: { children: ReactNode }) {
  return (
    <div style={{ minHeight: '100%', display: 'flex', flexDirection: 'column' }}>
      <header
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 'var(--space-4)',
          padding: 'var(--space-3) var(--space-5)',
          borderBottom: '1px solid var(--color-border)',
          background: 'var(--color-surface)',
        }}
      >
        <strong style={{ fontSize: '15px', letterSpacing: '-0.01em' }}>CommerceOps</strong>
        <ThemeToggle />
      </header>

      <main style={{ flex: 1, padding: 'var(--space-6) var(--space-5)' }}>{children}</main>
    </div>
  );
}
