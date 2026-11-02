import { Outlet, useLocation } from 'react-router'
import type { AppEnvironment } from '../environment'
import { FailOnPurposeError, readFail } from '../errors/failOnPurpose'

/**
 * Where the screens go, inside the frame. Its route has the error screen, so a screen that breaks shows
 * "Something went wrong" inside the normal frame (tab bar, sidebar) instead of replacing the whole app. QA and local
 * copies: `?fail=screen` breaks it on purpose.
 */
export function ScreenSlot({ environment }: { environment: AppEnvironment }) {
  const { search } = useLocation()
  if (readFail(environment.kind, search) === 'screen') {
    throw new FailOnPurposeError('screen')
  }
  return <Outlet />
}
