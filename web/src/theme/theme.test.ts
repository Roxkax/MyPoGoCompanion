import { describe, expect, it, vi } from 'vitest'
import { MemoryStorage } from '../testing/auth'
import earlyScript from '../../public/theme.js?raw'
import { applyTheme, readTheme, ThemeStore, themeKey, writeTheme } from './theme'

function brokenStorage(): Storage {
  const fail = () => {
    throw new Error('blocked')
  }
  return { getItem: fail, setItem: fail, removeItem: fail } as unknown as Storage
}

function pageWithBarColours() {
  const doc = document.implementation.createHTMLDocument('page')
  doc.head.innerHTML =
    '<meta name="theme-color" content="#F2F6F5" media="(prefers-color-scheme: light)">' +
    '<meta name="theme-color" content="#0D1514" media="(prefers-color-scheme: dark)">'
  const colours = () =>
    [...doc.querySelectorAll<HTMLMetaElement>('meta[name="theme-color"]')].map((m) => m.content)
  return { doc, colours }
}

describe('theme choice storage', () => {
  it('a new device follows the device', () => {
    expect(readTheme(new MemoryStorage())).toBe('device')
  })

  it('keeps Light and Dark; Follow device clears the entry', () => {
    const storage = new MemoryStorage()

    writeTheme('dark', storage)
    expect(storage.getItem(themeKey)).toBe('dark')
    expect(readTheme(storage)).toBe('dark')

    writeTheme('device', storage)
    expect(storage.getItem(themeKey)).toBeNull()
  })

  it('ignores anything else saved under the key', () => {
    const storage = new MemoryStorage()
    storage.setItem(themeKey, 'purple')

    expect(readTheme(storage)).toBe('device')
  })

  it('works without storage (private mode, blocked site data)', () => {
    expect(readTheme(brokenStorage())).toBe('device')
    expect(() => writeTheme('dark', brokenStorage())).not.toThrow()
    expect(readTheme(undefined)).toBe('device')
  })
})

describe('applyTheme', () => {
  it('Light and Dark mark the page and colour the browser bar to match', () => {
    const { doc, colours } = pageWithBarColours()

    applyTheme('dark', doc)
    expect(doc.documentElement.dataset.theme).toBe('dark')
    expect(colours()).toEqual(['#0D1514', '#0D1514'])

    applyTheme('light', doc)
    expect(doc.documentElement.dataset.theme).toBe('light')
    expect(colours()).toEqual(['#F2F6F5', '#F2F6F5'])
  })

  it('Follow device removes the mark and gives each device setting its own bar colour', () => {
    const { doc, colours } = pageWithBarColours()
    applyTheme('dark', doc)

    applyTheme('device', doc)

    expect(doc.documentElement.dataset.theme).toBeUndefined()
    expect(colours()).toEqual(['#F2F6F5', '#0D1514'])
  })
})

describe('ThemeStore', () => {
  it('changes the choice, saves it, marks the page and tells listeners', () => {
    const storage = new MemoryStorage()
    const { doc } = pageWithBarColours()
    const store = new ThemeStore(storage, doc)
    const listener = vi.fn()
    const stop = store.subscribe(listener)

    store.setChoice('light')

    expect(store.getChoice()).toBe('light')
    expect(storage.getItem(themeKey)).toBe('light')
    expect(doc.documentElement.dataset.theme).toBe('light')
    expect(listener).toHaveBeenCalledTimes(1)

    stop()
    store.setChoice('dark')
    expect(listener).toHaveBeenCalledTimes(1)
  })

  it('starts from what was saved and applies it', () => {
    const storage = new MemoryStorage()
    storage.setItem(themeKey, 'dark')
    const { doc } = pageWithBarColours()

    const store = new ThemeStore(storage, doc)
    store.apply()

    expect(store.getChoice()).toBe('dark')
    expect(doc.documentElement.dataset.theme).toBe('dark')
  })

  it('follows a change made in another tab', () => {
    const storage = new MemoryStorage()
    const { doc } = pageWithBarColours()
    const store = new ThemeStore(storage, doc)

    storage.setItem(themeKey, 'dark')
    store.handleStorageEvent({ key: 'mpgc.refreshToken' })
    expect(store.getChoice()).toBe('device')

    store.handleStorageEvent({ key: themeKey })
    expect(store.getChoice()).toBe('dark')

    storage.clear()
    store.handleStorageEvent({ key: null })
    expect(store.getChoice()).toBe('device')
  })
})

describe('public/theme.js (applies the choice before the page draws)', () => {
  function run(saved: string | null, storage: Storage = new MemoryStorage()) {
    if (saved) storage.setItem(themeKey, saved)
    const root = document.createElement('html')
    new Function('localStorage', 'document', earlyScript)(storage, { documentElement: root })
    return root.dataset.theme
  }

  it('uses the same key as the app', () => {
    expect(earlyScript).toContain(`'${themeKey}'`)
  })

  it.each([
    ['dark', 'dark'],
    ['light', 'light'],
    [null, undefined],
    ['purple', undefined],
  ])('saved %s → %s', (saved, theme) => {
    expect(run(saved)).toBe(theme)
  })

  it('does nothing when storage is blocked', () => {
    expect(run(null, brokenStorage())).toBeUndefined()
  })
})
