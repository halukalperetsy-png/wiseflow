import { HealthCard } from '../features/health/HealthCard';
import { AppShell } from './AppShell';
import { ThemeProvider } from './theme/ThemeProvider';

export function App() {
  return (
    <ThemeProvider>
      <AppShell>
        <HealthCard />
      </AppShell>
    </ThemeProvider>
  );
}
