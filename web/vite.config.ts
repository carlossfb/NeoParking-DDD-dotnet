import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@': path.resolve(__dirname, './src') } },
  // The API has no CORS: in dev the browser talks to Vite, which forwards /api to it.
  server: {
    proxy: {
      '/api': {
        target: process.env.API_URL ?? 'http://localhost:5114',
        rewrite: (p) => p.replace(/^\/api/, ''),
      },
    },
  },
})
