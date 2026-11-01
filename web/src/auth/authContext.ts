import { createContext, useContext } from 'react'
import type { AuthState, SessionManager } from './session'

/** Why leaving for Google failed: sign-in not set up on this server (503), or the API couldn't be reached. */
export type SignInProblem = 'notSetUp' | 'unreachable'

export interface SignInDialogState {
  open: boolean
  /** Where to come back to after Google (the page the person was on). */
  returnTo: string
}

export interface AuthContextValue {
  state: AuthState
  session: SessionManager
  dialog: SignInDialogState
  /** Opens the sign-in screen; after signing in the person comes back to `returnTo` (default: the current page). */
  openSignIn: (returnTo?: string) => void
  closeSignIn: () => void
  /** Leaves for Google's sign-in. Resolves only on failure (sign-in not set up, API unreachable). */
  continueWithGoogle: (returnTo?: string) => Promise<void>
  /** Why the last "Continue with Google" didn't leave, if it didn't (shown as `signIn.errors.<problem>`). */
  signInError: SignInProblem | null
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext)
  if (!value) {
    throw new Error('useAuth must be used inside <AuthProvider>')
  }
  return value
}

/** The page the person is on, as a path to come back to. */
export function currentPath(
  location: Pick<Location, 'pathname' | 'search' | 'hash'> = window.location,
): string {
  return `${location.pathname}${location.search}${location.hash}`
}
