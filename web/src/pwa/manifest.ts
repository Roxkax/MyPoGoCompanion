// The web app manifest: what a phone or computer needs to install the app (name, icon, full screen). Written at build
// time per copy of the app, so an installed QA copy is its own app with the orange icon and a "QA" name, never
// mistaken for the real one (decision 2026-11-01). Icons: public/icons/, drawn by scripts/make-icons.mjs.

import type { EnvironmentKind } from '../environmentKind.ts'

export interface ManifestIcon {
  src: string
  sizes: string
  type: string
  purpose?: string
}

export interface WebAppManifest {
  id: string
  name: string
  short_name: string
  description: string
  lang: string
  start_url: string
  scope: string
  display: 'standalone'
  background_color: string
  theme_color: string
  icons: ManifestIcon[]
}

const appName = 'MyPoGoCompanion'

/** The light background (--bg in styles/tokens.css): the splash screen Android draws while the app starts. */
export const splashBackground = '#F2F6F5'

/** Icon files of this copy of the app: production has none, QA "qa-", local "local-". */
export function iconPrefix(kind: EnvironmentKind): string {
  return kind === 'production' ? '/icons/' : `/icons/${kind}-`
}

/** The icon iOS puts on the home screen (`<link rel="apple-touch-icon">`). */
export function appleTouchIcon(kind: EnvironmentKind): string {
  return `${iconPrefix(kind)}apple-touch-icon.png`
}

/** The name under the icon: short enough for a phone's home screen. */
export function shortName(kind: EnvironmentKind): string {
  return { production: 'MyPoGo', qa: 'QA MyPoGo', local: 'Local MyPoGo' }[kind]
}

export function webAppManifest(kind: EnvironmentKind): WebAppManifest {
  const icons = iconPrefix(kind)
  return {
    id: '/',
    name: kind === 'production' ? appName : `${kind === 'qa' ? 'QA' : 'Local'} · ${appName}`,
    short_name: shortName(kind),
    description:
      'A free companion for Pokémon GO: Pokédex, raids, tier lists, search filters and your collection.',
    lang: 'en',
    start_url: '/',
    scope: '/',
    display: 'standalone',
    background_color: splashBackground,
    theme_color: splashBackground,
    icons: [
      { src: `${icons}icon-192.png`, sizes: '192x192', type: 'image/png' },
      { src: `${icons}icon-512.png`, sizes: '512x512', type: 'image/png' },
      { src: `${icons}maskable-512.png`, sizes: '512x512', type: 'image/png', purpose: 'maskable' },
    ],
  }
}
