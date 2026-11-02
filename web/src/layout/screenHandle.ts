import { useMatches } from 'react-router'
import type { DataKind } from '../freshness/freshness'
import type { TitleKey } from '../i18n/keys'

/** What a route tells the shell about its screen (React Router's `handle`). */
export interface ScreenHandle {
  /** The browser tab's title for the screen, titles.<title> (Today shows just the app's name). */
  title?: TitleKey
  /** Settings and Admin hide the phone's tab bar (approved screens F2, G1, G2). */
  hideTabBar?: boolean
  /**
   * The kinds of data the screen shows: its frame puts the out-of-date warning at the top of the content when one
   * stops updating (E01-F07). `game` is the daily import, `live` raids and events.
   */
  data?: readonly DataKind[]
  /** A personal screen (sign-in needed): no warning above the sign-in prompt that replaces it when signed out. */
  personal?: boolean
}

/** The current screen's handle: every matching route's, the deepest winning. */
export function useScreenHandle(): ScreenHandle {
  const matches = useMatches()
  return matches.reduce<ScreenHandle>(
    (handle, match) => ({ ...handle, ...(match.handle as ScreenHandle | undefined) }),
    {},
  )
}
