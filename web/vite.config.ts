/// <reference types="vitest/config" />
import { readFileSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv, type Plugin } from 'vite'
import { withContentSecurityPolicy } from './src/security/contentSecurityPolicy.ts'

/** Writes the Content Security Policy for this build's API address into dist/staticwebapp.config.json. */
function contentSecurityPolicy(apiUrl: string | undefined): Plugin {
  return {
    name: 'content-security-policy',
    apply: 'build',
    writeBundle(options) {
      const file = resolve(options.dir ?? 'dist', 'staticwebapp.config.json')
      writeFileSync(file, withContentSecurityPolicy(readFileSync(file, 'utf8'), apiUrl))
    },
  }
}

export default defineConfig(({ mode }) => ({
  plugins: [react(), contentSecurityPolicy(loadEnv(mode, process.cwd(), 'VITE_').VITE_API_URL)],
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
        'src/testing/**',
      ],
      reporter: ['text', 'cobertura', 'json-summary'],
      reportsDirectory: 'coverage',
      // Minimum agreed for the build-and-unit check; below it `npm run test:coverage` fails.
      thresholds: { lines: 90, branches: 90, functions: 90, statements: 90 },
    },
  },
}))
