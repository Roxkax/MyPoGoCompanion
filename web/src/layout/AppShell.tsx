import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Outlet, ScrollRestoration, useMatches } from 'react-router'
import { SignInDialog } from '../components/SignInDialog'
import { EnvironmentBanner } from '../components/EnvironmentBanner'
import type { AppEnvironment } from '../environment'
import type { TitleKey } from '../i18n/keys'
import { InstallHint } from '../pwa/InstallHint'
import { useConnection } from '../pwa/pwaContext'
import { OfflineScreen } from '../screens/OfflineScreen'
import { Overlays } from './Overlays'
import { useOverlay } from './overlay'
import { Sidebar } from './Sidebar'
import { TabBar } from './TabBar'
import { TopBar } from './TopBar'
import { useIsDesktop } from './useMediaQuery'

/** What a route tells the shell about its screen (React Router's `handle`). */
export interface ScreenHandle {
  /** The browser tab's title for the screen, titles.<title> (Today shows just the app's name). */
  title?: TitleKey
  /** Settings and Admin hide the phone's tab bar (approved screens F2, G1, G2). */
  hideTabBar?: boolean
}

function useScreenHandle(): ScreenHandle {
  const matches = useMatches()
  return matches.reduce<ScreenHandle>(
    (handle, match) => ({ ...handle, ...(match.handle as ScreenHandle | undefined) }),
    {},
  )
}

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
  )
}
