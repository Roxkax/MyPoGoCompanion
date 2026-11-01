// Settings → Theme (screen F2): follow the device, or always light, or always dark. Saved per device in the browser
// (decision 2026-11-01), so it works the same signed in or out; a new device starts on "Follow device".
// public/theme.js applies the saved choice before the page draws, so a dark choice never flashes light first.

export type ThemeChoice = 'device' | 'light' | 'dark'

export const themeChoices: readonly ThemeChoice[] = ['device', 'light', 'dark']

export const themeKey = 'mpgc.theme'

/** The browser bar's colour (meta theme-color) for each theme: the page background. */
export const barColour = { light: '#F2F6F5', dark: '#0D1514' } as const

function isChoice(value: unknown): value is ThemeChoice {
  return themeChoices.includes(value as ThemeChoice)
}

export function readTheme(storage: Storage | undefined = globalThis.localStorage): ThemeChoice {
  try {
    const value = storage?.getItem(themeKey)
    return isChoice(value) ? value : 'device'
  } catch {
    return 'device'
  }
}

export function writeTheme(
  choice: ThemeChoice,
  storage: Storage | undefined = globalThis.localStorage,
): void {
  try {
    if (choice === 'device') {
      storage?.removeItem(themeKey)
    } else {
      storage?.setItem(themeKey, choice)
    }
  } catch {
    // Storage unavailable (private mode, blocked site data): the choice lasts until the page closes.
  }
}

/** Marks <html> with the choice and points the browser bar at the matching colour. */
export function applyTheme(choice: ThemeChoice, doc: Document = document): void {
  const root = doc.documentElement
  if (choice === 'device') {
    delete root.dataset.theme
  } else {
    root.dataset.theme = choice
  }
  doc.querySelectorAll<HTMLMetaElement>('meta[name="theme-color"]').forEach((meta) => {
    // Each tag is for one device setting (media="(prefers-color-scheme: …)"); a fixed choice overrides both.
    const forDark = (meta.getAttribute('media') ?? '').includes('dark')
    const shown = choice === 'device' ? (forDark ? 'dark' : 'light') : choice
    meta.content = barColour[shown]
  })
}

/** The theme choice for this device: read, change, and follow changes made in another tab. */
export class ThemeStore {
  private choice: ThemeChoice
  private readonly listeners = new Set<() => void>()
  private readonly storage: Storage | undefined
  private readonly doc: Document

  constructor(storage: Storage | undefined = globalThis.localStorage, doc: Document = document) {
    this.storage = storage
    this.doc = doc
    this.choice = readTheme(storage)
  }

  getChoice = (): ThemeChoice => this.choice

  setChoice = (choice: ThemeChoice): void => {
    writeTheme(choice, this.storage)
    this.update(choice)
  }

  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }

  /** Applies the current choice to the page (at start-up). */
  apply = (): void => applyTheme(this.choice, this.doc)

  /** Another tab changed the theme: follow it. */
  handleStorageEvent = (event: Pick<StorageEvent, 'key'>): void => {
    if (event.key === themeKey || event.key === null) {
      this.update(readTheme(this.storage))
    }
  }

  private update(choice: ThemeChoice) {
    this.choice = choice
    applyTheme(choice, this.doc)
    this.listeners.forEach((listener) => listener())
  }
}
