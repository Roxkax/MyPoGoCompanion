import i18next, { type i18n } from 'i18next'
import type { Language } from './languages'
import { en } from './messages/en'
import { ptPT } from './messages/pt-PT'

declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'translation'
    resources: { translation: typeof en }
  }
}

/** Every text in the interface, per language (src/i18n/messages). */
export const resources = {
  en: { translation: en },
  'pt-PT': { translation: ptPT },
} as const

/**
 * A ready i18next instance showing `language`, given to the screens by LanguageProvider. Both languages are in the
 * bundle (a few KB), so switching is instant and works offline. React's escaping already protects the text, so
 * i18next doesn't escape it again.
 */
export function createI18n(language: Language): i18n {
  const instance = i18next.createInstance()
  void instance.init({
    resources,
    lng: language,
    fallbackLng: 'en',
    supportedLngs: ['en', 'pt-PT'],
    initAsync: false,
    interpolation: { escapeValue: false },
    react: { useSuspense: false },
  })
  return instance
}
