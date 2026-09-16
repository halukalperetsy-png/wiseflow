import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      // Same-origin in development, so no CORS policy is needed on the API and
      // the session cookie is a first-party cookie for the dev server too.
      '/health': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
});
