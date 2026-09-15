import { useTheme } from './useTheme';
import type { ThemePreference } from './themeContext';

const OPTIONS: { value: ThemePreference; label: string; icon: string }[] = [
  { value: 'light', label: 'Light', icon: '☀' },
  { value: 'dark', label: 'Dark', icon: '☾' },
  { value: 'system', label: 'System', icon: '◐' },
];

export function ThemeToggle() {
  const { preference, setPreference } = useTheme();

  return (
    <div role="group" aria-label="Theme" style={{ display: 'flex', gap: 'var(--space-1)' }}>
      {OPTIONS.map((option) => {
        const selected = preference === option.value;
        return (
          <button
            key={option.value}
            type="button"
            aria-pressed={selected}
            onClick={() => setPreference(option.value)}
            style={{
              borderColor: selected ? 'var(--color-accent)' : 'var(--color-border)',
              color: selected ? 'var(--color-accent)' : 'var(--color-text-muted)',
            }}
          >
            <span aria-hidden="true">{option.icon}</span> {option.label}
          </button>
        );
      })}
    </div>
  );
}
