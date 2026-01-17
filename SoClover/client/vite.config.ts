import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  base: '/',
  build: {
    outDir: 'dist',
    assetsDir: 'assets',
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
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
