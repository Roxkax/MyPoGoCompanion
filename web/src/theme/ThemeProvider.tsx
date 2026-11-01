import { useEffect, useState, useSyncExternalStore, type ReactNode } from 'react'
import { ThemeContext } from './themeContext'
import { ThemeStore } from './theme'

/** Holds this device's theme choice, applies it to the page and follows changes made in other tabs. */
export function ThemeProvider({ store, children }: { store?: ThemeStore; children: ReactNode }) {
  const [themes] = useState(() => store ?? new ThemeStore())
  const choice = useSyncExternalStore(themes.subscribe, themes.getChoice)

  useEffect(() => {
    themes.apply()
    const onStorage = (event: StorageEvent) => themes.handleStorageEvent(event)
    window.addEventListener('storage', onStorage)
    return () => window.removeEventListener('storage', onStorage)
  }, [themes])

  return (
    <ThemeContext.Provider value={{ choice, setChoice: themes.setChoice }}>
      {children}
    </ThemeContext.Provider>
  )
}
