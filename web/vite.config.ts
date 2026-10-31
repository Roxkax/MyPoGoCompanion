/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true },
  preview: { port: 4173, strictPort: true },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test-setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    coverage: {
      provider: 'v8',
      // Every source file counts, including ones no test imports yet.
      include: ['src/**/*.{ts,tsx}'],
      exclude: [
        'src/**/*.test.{ts,tsx}',
        'src/**/*.d.ts',
        'src/test-setup.ts',
        'src/main.tsx',
        'src/api/generated/**',
      ],
      reporter: ['text', 'cobertura', 'json-summary'],
      reportsDirectory: 'coverage',
      // Minimum agreed for the build-and-unit check; below it `npm run test:coverage` fails.
      thresholds: { lines: 90, branches: 90, functions: 90, statements: 90 },
    },
  },
})
