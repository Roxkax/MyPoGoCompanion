import { useEffect, useMemo, useState, useSyncExternalStore, type ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { LanguageStore } from './language'
import { LanguageContext } from './languageContext'

/** Holds this device's language, gives every screen its texts (useTranslation) and follows other tabs. */
export function LanguageProvider({
  store,
  children,
}: {
  store?: LanguageStore
  children: ReactNode
}) {
  const [languages] = useState(() => store ?? new LanguageStore())
  const language = useSyncExternalStore(languages.subscribe, languages.getLanguage)

  useEffect(() => {
    languages.apply()
    const onStorage = (event: StorageEvent) => languages.handleStorageEvent(event)
    window.addEventListener('storage', onStorage)
    return () => window.removeEventListener('storage', onStorage)
  }, [languages])

  const value = useMemo(
    () => ({ language, setLanguage: languages.setLanguage }),
    [language, languages],
  )
  return (
    <I18nextProvider i18n={languages.i18n}>
      <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>
    </I18nextProvider>
  )
}
