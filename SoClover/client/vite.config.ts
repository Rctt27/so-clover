import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import path from 'path'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  base: '/',
  envDir: '../',
  build: {
    outDir: 'dist',
    assetsDir: 'assets',
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  test: {
    environment: 'node',
    // Cadrer vitest sur les tests unitaires de `src/`. Les specs Playwright (`e2e/*.spec.ts`)
    // ont leur propre runner (`npm run test:e2e`) et ne doivent pas être collectées ici —
    // sinon `test()` de @playwright/test échoue (« did not expect test() to be called here »).
    include: ['src/**/*.test.{ts,tsx}'],
  },
  server: {
    proxy: {
      '/hubs': {
        target: 'http://localhost:5000', // À ajuster selon le port de l'API ASP.NET
        ws: true,
      },
      '/api': 'http://localhost:5000',
    },
  },
})
