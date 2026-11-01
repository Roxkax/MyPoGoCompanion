import { createContext, useContext, useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { formatsFor, type Formats } from './format'
import { isLanguage, type Language } from './languages'

export interface LanguageContextValue {
  language: Language
  /** Shows a language and saves it on this device (Settings uses useLanguageChoice, which also saves it with the account). */
  setLanguage: (language: Language) => void
}

export const LanguageContext = createContext<LanguageContextValue | null>(null)

export function useLanguage(): LanguageContextValue {
  const value = useContext(LanguageContext)
  if (!value) {
    throw new Error('useLanguage must be used inside <LanguageProvider>')
  }
  return value
}

/** The language the screens are showing (from i18next, so it works wherever the texts do). */
export function useCurrentLanguage(): Language {
  const { i18n } = useTranslation()
  return isLanguage(i18n.language) ? i18n.language : 'en'
}

/** Dates and numbers in the language the screens are showing. */
export function useFormats(): Formats {
  const language = useCurrentLanguage()
  return useMemo(() => formatsFor(language), [language])
}
