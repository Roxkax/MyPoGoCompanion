import type { ReactNode } from 'react'
import { useAuth } from '../auth/authContext'
import { isAdmin } from '../auth/roles'
import { SignedOutPrompt, type PersonalFeature } from '../components/SignedOutPrompt'
import { NotFound } from '../screens/NotFoundScreen'

/** A personal screen (collection, saved filters): the sign-in prompt in its place when nobody is signed in (F1). */
export function SignedInOnly({
  feature,
  children,
}: {
  feature: PersonalFeature
  children: ReactNode
}) {
  const { state } = useAuth()
  if (state.status === 'loading') {
    return null
  }
  return state.status === 'signedIn' ? children : <SignedOutPrompt feature={feature} />
}

/**
 * Admin screens: anyone who isn't an admin gets the ordinary "Page not found", so the screens' existence isn't
 * shown (decision 2026-11-01). The API checks the role again on every admin request.
 */
export function AdminOnly({ children }: { children: ReactNode }) {
  const { state } = useAuth()
  if (state.status === 'loading') {
    return null
  }
  return state.status === 'signedIn' && isAdmin(state.user.role) ? children : <NotFound />
}
