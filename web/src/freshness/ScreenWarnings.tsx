import { useAuth } from '../auth/authContext'
import { useScreenHandle } from '../layout/screenHandle'
import { StaleWarnings } from './StaleWarnings'

/**
 * The out-of-date warning for the screen being shown, from the kinds of data its route names (`handle.data`). The
 * screen frames (TabScreen, DetailScreen) put it at the top of the content, so every screen built later gets it.
 */
export function ScreenWarnings() {
  const handle = useScreenHandle()
  const { state } = useAuth()
  if (!handle.data?.length || (handle.personal && state.status !== 'signedIn')) {
    return null
  }
  return <StaleWarnings kinds={handle.data} />
}
