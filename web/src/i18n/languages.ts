// The app's interface languages (decision 2026-10-27) and where the choice is kept (decision 2026-11-01): on the
// device always, and with the account when signed in. First visit: any Portuguese in the browser's languages gives
// Português (Portugal), anything else English.

export type Language = 'en' | 'pt-PT'

export const languages: readonly Language[] = ['en', 'pt-PT']

/** Each language's name in itself, so anyone can find their own whatever the app is showing. */
export const languageNames: Record<Language, string> = {
  en: 'English',
  'pt-PT': 'Português (Portugal)',
}

/** The locale used for dates and numbers: British English ("Tue 27 Oct 2026, 18:30"), Portugal's Portuguese. */
export const locales: Record<Language, string> = { en: 'en-GB', 'pt-PT': 'pt-PT' }

export const languageKey = 'mpgc.language'

export function isLanguage(value: unknown): value is Language {
  return languages.includes(value as Language)
}

/** The first of the browser's languages that is English or Portuguese decides; neither means English. */
export function browserLanguage(preferred: readonly string[] = browserPreferences()): Language {
  for (const tag of preferred) {
    const base = tag.toLowerCase().split('-')[0]
    if (base === 'pt') return 'pt-PT'
    if (base === 'en') return 'en'
  }
  return 'en'
}

function browserPreferences(): readonly string[] {
  if (typeof navigator === 'undefined') {
    return []
  }
  return navigator.languages?.length ? navigator.languages : [navigator.language]
}

/** The language saved on this device, or the browser's when nothing (valid) is saved. */
export function readLanguage(
  storage: Storage | undefined = globalThis.localStorage,
  preferred?: readonly string[],
): Language {
  try {
    const value = storage?.getItem(languageKey)
    if (isLanguage(value)) {
      return value
    }
  } catch {
    // Storage unavailable: fall through to the browser's language.
  }
  return browserLanguage(preferred)
}

export function writeLanguage(
  language: Language,
  storage: Storage | undefined = globalThis.localStorage,
): void {
  try {
    storage?.setItem(languageKey, language)
  } catch {
    // Storage unavailable (private mode, blocked site data): the choice lasts until the page closes.
  }
}
