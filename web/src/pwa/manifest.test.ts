import { existsSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { appleTouchIcon, shortName, splashBackground, webAppManifest } from './manifest'

/** A file in web/public, by its address ("/icons/icon-192.png"). */
const publicFolder = resolve(dirname(fileURLToPath(import.meta.url)), '../../public')
const publicFile = (src: string) => `${publicFolder}${src}`

describe('web app manifest', () => {
  it('installs production as MyPoGoCompanion, full screen, from the start page', () => {
    const manifest = webAppManifest('production')
    expect(manifest.name).toBe('MyPoGoCompanion')
    expect(manifest.short_name).toBe('MyPoGo')
    expect(manifest.display).toBe('standalone')
    expect(manifest.start_url).toBe('/')
    expect(manifest.scope).toBe('/')
    expect(manifest.id).toBe('/')
    expect(manifest.icons.map((icon) => icon.src)).toEqual([
      '/icons/icon-192.png',
      '/icons/icon-512.png',
      '/icons/maskable-512.png',
    ])
  })

  it('makes QA and local copies their own apps, with their own icon and name', () => {
    expect(webAppManifest('qa').name).toBe('QA · MyPoGoCompanion')
    expect(webAppManifest('qa').icons[0]!.src).toBe('/icons/qa-icon-192.png')
    expect(webAppManifest('local').name).toBe('Local · MyPoGoCompanion')
    expect(webAppManifest('local').icons[2]!.src).toBe('/icons/local-maskable-512.png')
    expect(shortName('qa')).toBe('QA MyPoGo')
    expect(appleTouchIcon('qa')).toBe('/icons/qa-apple-touch-icon.png')
    expect(appleTouchIcon('production')).toBe('/icons/apple-touch-icon.png')
  })

  it('has a 192 and a 512 icon and a maskable one, as Android asks', () => {
    const icons = webAppManifest('production').icons
    expect(icons.some((icon) => icon.sizes === '192x192')).toBe(true)
    expect(icons.some((icon) => icon.sizes === '512x512' && !icon.purpose)).toBe(true)
    expect(icons.some((icon) => icon.purpose === 'maskable')).toBe(true)
  })

  it('points only at icon files that exist, for every copy', () => {
    for (const kind of ['production', 'qa', 'local'] as const) {
      for (const src of [
        ...webAppManifest(kind).icons.map((icon) => icon.src),
        appleTouchIcon(kind),
      ]) {
        expect(existsSync(publicFile(src)), src).toBe(true)
      }
    }
  })

  it('starts on the app background, so the splash screen matches the app', () => {
    expect(webAppManifest('production').background_color).toBe(splashBackground)
    expect(splashBackground.toLowerCase()).toBe('#f2f6f5')
  })
})
