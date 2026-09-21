import react from '@vitejs/plugin-react'
import { resolve } from 'node:path'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  base: '/hanging-post/',
  build: {
    emptyOutDir: true,
    outDir: resolve(import.meta.dirname, '../Farm/wwwroot/hanging-post'),
    rollupOptions: {
      output: {
        assetFileNames: 'assets/[name][extname]',
        entryFileNames: 'assets/hanging-post.js',
      },
    },
  },
})
