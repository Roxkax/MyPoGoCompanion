import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render } from '@testing-library/react'
import { App } from '../App'
import type { Me } from '../auth/authApi'
import { AuthProvider } from '../auth/AuthProvider'
import type { SessionManager } from '../auth/session'
import { readEnvironment, type AppEnvironment } from '../environment'
import { desktopQuery } from '../layout/useMediaQuery'
import { ThemeProvider } from '../theme/ThemeProvider'
import { ThemeStore } from '../theme/theme'
import { LanguageStore } from '../i18n/language'
import { LanguageProvider } from '../i18n/LanguageProvider'
import { ConnectionWatch } from '../pwa/connection'
import { InstallWatch } from '../pwa/install'
import { InstallHintStore } from '../pwa/installHint'
import type { Pwa } from '../pwa/pwaContext'
import { PwaProvider } from '../pwa/PwaProvider'
import { ash, MemoryStorage, sessionFor, testSession } from './auth'

/**
 * A pretend screen size: window.matchMedia answers the desktop query as asked, and resize() changes it later (as
 * when a window is resized), telling everything listening. Removed after each test (src/test-setup.ts).
 */
export function setViewport(layout: 'phone' | 'desktop') {
  let desktop = layout === 'desktop'
  const listeners = new Set<() => void>()
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: (query: string) => ({
      get matches() {
        return query === desktopQuery && desktop
      },
      media: query,
      addEventListener: (_: string, listener: () => void) => listeners.add(listener),
      removeEventListener: (_: string, listener: () => void) => listeners.delete(listener),
    }),
  })
  return {
    resize(to: 'phone' | 'desktop') {
      desktop = to === 'desktop'
      act(() => listeners.forEach((listener) => listener()))
    },
  }
}

interface AppOptions {
  layout?: 'phone' | 'desktop'
  /** Signed in as this person; signed out when left out. */
  user?: Me
  session?: SessionManager
  environment?: AppEnvironment
  themes?: ThemeStore
  /** This device's language (English, nothing saved, when left out). */
  languages?: LanguageStore
  navigate?: (url: string) => void
  /** Install, hint and connection; online, not installable, first visit when left out. */
  pwa?: Partial<Pwa>
}

/**
 * The installable app's parts for a test: online (the site always answers, reload does nothing), nothing announced
 * by the browser, and the hint's storage in memory. Pass your own to test going offline or installing.
 */
export function testPwa(parts: Partial<Pwa> = {}): Pwa {
  return {
    install:
      parts.install ??
      new InstallWatch(window as unknown as ConstructorParameters<typeof InstallWatch>[0]),
    hint: parts.hint ?? new InstallHintStore(new MemoryStorage(), new MemoryStorage()),
    connection:
      parts.connection ??
      new ConnectionWatch({ check: () => Promise.resolve(true), reload: () => {} }),
  }
}

/** The whole app at an address, as the browser shows it: providers, router, shell and screen. */
export function renderApp(path = '/', options: AppOptions = {}) {
  const viewport = setViewport(options.layout ?? 'phone')
  window.history.replaceState(null, '', path)
  const { session } = options.session ? { session: options.session } : testSession()
  const themes = options.themes ?? new ThemeStore(new MemoryStorage())
  const languages = options.languages ?? new LanguageStore(new MemoryStorage(), ['en-GB'])
  const pwa = testPwa(options.pwa)
  const result = render(
    <ThemeProvider store={themes}>
      <LanguageProvider store={languages}>
        <QueryClientProvider
          client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
        >
          <AuthProvider
            session={session}
            navigate={options.navigate ?? (() => {})}
            pathname={new URL(path, 'http://localhost').pathname}
          >
            <PwaProvider pwa={pwa}>
              <App
                environment={options.environment ?? readEnvironment({ VITE_APP_ENV: 'production' })}
              />
            </PwaProvider>
          </AuthProvider>
        </QueryClientProvider>
      </LanguageProvider>
    </ThemeProvider>,
  )
  if (options.user) {
    act(() => session.accept(sessionFor(options.user)))
  }
  return { ...result, session, themes, languages, viewport, pwa }
}

export const admin: Me = { ...ash, role: 'Admin' }

/** The address the app is showing now. */
export function currentAddress(): string {
  return window.location.pathname + window.location.search
}
