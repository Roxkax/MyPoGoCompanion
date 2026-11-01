/// <reference types="vitest/config" />
import { readFileSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv, type Plugin } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'
import { environmentKind, type EnvironmentKind } from './src/environmentKind.ts'
import { appleTouchIcon, webAppManifest } from './src/pwa/manifest.ts'
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

/** iOS reads the home-screen icon and name from the page, not the manifest: this copy's own (QA orange, local grey). */
function appleHomeScreen(kind: EnvironmentKind, name: string): Plugin {
  return {
    name: 'apple-home-screen',
    transformIndexHtml: () => [
      {
        tag: 'link',
        attrs: { rel: 'apple-touch-icon', href: appleTouchIcon(kind) },
        injectTo: 'head',
      },
      {
        tag: 'meta',
        attrs: { name: 'apple-mobile-web-app-title', content: name },
        injectTo: 'head',
      },
    ],
  }
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_')
  const kind = environmentKind(env.VITE_APP_ENV)
  const manifest = webAppManifest(kind)
  return {
    plugins: [
      react(),
      contentSecurityPolicy(env.VITE_API_URL),
      appleHomeScreen(kind, manifest.short_name),
      // Installable app (E01-F06): the manifest for this copy, and the service worker built from src/sw.ts with the
      // list of the build's files (src/pwa/worker.ts). Registered by src/pwa/register.ts, so no script is injected.
      VitePWA({
        strategies: 'injectManifest',
        srcDir: 'src',
        filename: 'sw.ts',
        injectRegister: false,
        manifest,
        manifestFilename: 'manifest.webmanifest',
        injectManifest: {
          // What the app needs to open: its page, scripts, styles, the Latin fonts, theme.js and the tab icons.
          // Fonts for other alphabets are fetched when a page needs them; the home-screen PNGs only when installing.
          globPatterns: [
            'index.html',
            'theme.js',
            'icon*.svg',
            'assets/*.{js,css}',
            'assets/*latin-*.woff2',
          ],
          globIgnores: ['assets/*-ext-*.woff2'],
        },
      }),
    ],
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
          'src/sw.ts',
          'src/api/generated/**',
          'src/testing/**',
        ],
        reporter: ['text', 'cobertura', 'json-summary'],
        reportsDirectory: 'coverage',
        // Minimum agreed for the build-and-unit check; below it `npm run test:coverage` fails.
        thresholds: { lines: 90, branches: 90, functions: 90, statements: 90 },
      },
    },
  }
})
