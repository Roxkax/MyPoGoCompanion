import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

// Static Web Apps' settings for the installable app (public/staticwebapp.config.json).
const config = JSON.parse(
  readFileSync(
    resolve(dirname(fileURLToPath(import.meta.url)), '../../public/staticwebapp.config.json'),
    'utf8',
  ),
) as {
  navigationFallback: { exclude: string[] }
  mimeTypes: Record<string, string>
  routes: { route: string; headers: Record<string, string> }[]
}

const indexHtml = readFileSync(
  resolve(dirname(fileURLToPath(import.meta.url)), '../../index.html'),
  'utf8',
)

const headersOf = (route: string) => config.routes.find((entry) => entry.route === route)?.headers

describe('hosting the installable app', () => {
  it('never lets the browser keep an old service worker or manifest', () => {
    expect(headersOf('/sw.js')?.['Cache-Control']).toBe('no-cache')
    expect(headersOf('/manifest.webmanifest')?.['Cache-Control']).toBe('no-cache')
  })

  it('serves the manifest as a manifest', () => {
    expect(config.mimeTypes['.webmanifest']).toBe('application/manifest+json')
  })

  it('answers a missing script, icon or manifest with "not found", never with the app page', () => {
    expect(config.navigationFallback.exclude).toContain('/icons/*')
    expect(config.navigationFallback.exclude.some((pattern) => pattern.includes('js'))).toBe(true)
    expect(
      config.navigationFallback.exclude.some((pattern) => pattern.includes('webmanifest')),
    ).toBe(true)
  })

  it('lets the installed app use the whole screen, so the bars can keep clear of the phone’s own', () => {
    // Without viewport-fit=cover, iOS reports no safe areas and the tab bar sits under the home indicator.
    expect(indexHtml).toMatch(/<meta name="viewport" content="[^"]*viewport-fit=cover/)
  })
})
