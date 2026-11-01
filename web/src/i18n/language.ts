import type { i18n } from 'i18next'
import { createI18n } from './i18n'
import { languageKey, readLanguage, writeLanguage, type Language } from './languages'

/**
 * This device's interface language: read, change, follow changes made in another tab, and keep i18next and
 * <html lang> in step. The account's language is applied on top by AccountLanguage once someone is signed in.
 */
export class LanguageStore {
  readonly i18n: i18n
  private language: Language
  private readonly listeners = new Set<() => void>()
  private readonly storage: Storage | undefined
  private readonly preferred: readonly string[] | undefined
  private readonly doc: Document

  constructor(
    storage: Storage | undefined = globalThis.localStorage,
    preferred?: readonly string[],
    doc: Document = document,
  ) {
    this.storage = storage
    this.preferred = preferred
    this.doc = doc
    this.language = readLanguage(storage, preferred)
    this.i18n = createI18n(this.language)
  }

  getLanguage = (): Language => this.language

  /** Shows `language` at once and saves it on this device. */
  setLanguage = (language: Language): void => {
    writeLanguage(language, this.storage)
    this.update(language)
  }

  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }

  /** Marks the page with the current language (at start-up), for screen readers, spelling and hyphenation. */
  apply = (): void => {
    this.doc.documentElement.lang = this.language
  }

  /** Another tab changed the language: follow it. */
  handleStorageEvent = (event: Pick<StorageEvent, 'key'>): void => {
    if (event.key === languageKey || event.key === null) {
      this.update(readLanguage(this.storage, this.preferred))
    }
  }

  private update(language: Language) {
    if (language === this.language) {
      return
    }
    this.language = language
    void this.i18n.changeLanguage(language)
    this.apply()
    this.listeners.forEach((listener) => listener())
  }
}
