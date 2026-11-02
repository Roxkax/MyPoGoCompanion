import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Outlet, ScrollRestoration, useLocation } from 'react-router'
import { SignInDialog } from '../components/SignInDialog'
import { EnvironmentBanner } from '../components/EnvironmentBanner'
import type { AppEnvironment } from '../environment'
import { readOverride } from '../freshness/freshness'
import { StaleOverrideContext } from '../freshness/freshnessContext'
import { storageOrNull } from '../pwa/installHint'
import { InstallHint } from '../pwa/InstallHint'
import { useConnection } from '../pwa/pwaContext'
import { OfflineScreen } from '../screens/OfflineScreen'
import { Overlays } from './Overlays'
import { useOverlay } from './overlay'
import { Sidebar } from './Sidebar'
import { TabBar } from './TabBar'
import { TopBar } from './TopBar'
import { useScreenHandle, type ScreenHandle } from './screenHandle'
import { useIsDesktop } from './useMediaQuery'

export type { ScreenHandle }

/**
 * The frame around every screen. Phone: the screen (with its own header) and the bottom tab bar. Desktop: the
 * sidebar and the top bar around the screen. Both: the QA/local strip, the sign-in window, search and Ask. Without a
 * connection the "You're offline" screen (H3) takes the screen's place; the phone also has the one-time install hint.
 */
export function AppShell({ environment }: { environment: AppEnvironment }) {
  const desktop = useIsDesktop()
  const handle = useScreenHandle()
  const { open, current } = useOverlay()
  const { t } = useTranslation()
  const { offline } = useConnection()
  const title = handle.title && t(`titles.${handle.title}`)
  const screen = offline ? <OfflineScreen /> : <Outlet />
  const stale = useStaleOverrideSetting(environment)

  useEffect(() => {
    document.title = title ? `${title} · ${environment.title}` : environment.title
  }, [title, environment.title])

  // Ctrl K / ⌘ K opens search from anywhere.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        if (current !== 'search') {
          open('search')
        }
      }
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [open, current])

  return (
    <StaleOverrideContext value={stale}>
      <div className="app" data-layout={desktop ? 'desktop' : 'phone'}>
        <EnvironmentBanner environment={environment} />
        {desktop ? (
          <div className="desktop">
            <Sidebar environment={environment} />
            <div className="desktop-main">
              <TopBar />
              {screen}
            </div>
          </div>
        ) : (
          <div className="phone" data-tab-bar={handle.hideTabBar ? undefined : true}>
            {screen}
            {!handle.hideTabBar && <InstallHint icon={environment.icon} />}
            {!handle.hideTabBar && <TabBar />}
          </div>
        )}
        <Overlays />
        <SignInDialog />
        <ScrollRestoration />
      </div>
    </StaleOverrideContext>
  )
}

/** QA and local copies: `?stale=` pretends data is out of date for this browser tab (see freshness.ts). */
function useStaleOverrideSetting(environment: AppEnvironment) {
  const { search } = useLocation()
  const [storage] = useState(() => storageOrNull(() => window.sessionStorage))
  const [override, setOverride] = useState(() => readOverride(environment.kind, search, storage))
  const [seen, setSeen] = useState(search)
  if (search !== seen) {
    setSeen(search)
    setOverride(readOverride(environment.kind, search, storage))
  }
  return override
}
