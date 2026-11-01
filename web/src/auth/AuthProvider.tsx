import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from 'react'
import { api, bearerToken } from '../api/client'
import { ApiError } from './authApi'
import {
  AuthContext,
  currentPath,
  type AuthContextValue,
  type SignInDialogState,
  type SignInProblem,
} from './authContext'
import { buildGoogleSignInUrl, callbackPath } from './google'
import { SessionManager } from './session'

export interface AuthProviderProps {
  children: ReactNode
  session?: SessionManager
  /** Leaves the app for Google; replaceable in tests. */
  navigate?: (url: string) => void
  /** The current path; the callback page finishes the sign-in itself instead of starting a session. */
  pathname?: string
}

export function AuthProvider({
  children,
  session: given,
  navigate = (url) => window.location.assign(url),
  pathname = window.location.pathname,
}: AuthProviderProps) {
  const [session] = useState(() => given ?? new SessionManager())
  // The page the app opened on: the callback page finishes the sign-in itself, and leaving it must not start again.
  const [openedOn] = useState(pathname)
  const state = useSyncExternalStore(session.subscribe, session.getState)
  const [dialog, setDialog] = useState<SignInDialogState>({ open: false, returnTo: '/' })
  const [signInError, setSignInError] = useState<SignInProblem | null>(null)

  useEffect(() => {
    if (openedOn !== callbackPath) {
      void session.start()
    }
    const onStorage = (event: StorageEvent) => session.handleStorageEvent(event)
    window.addEventListener('storage', onStorage)
    return () => window.removeEventListener('storage', onStorage)
  }, [session, openedOn])

  // Every API call that needs it gets this session's access token.
  useEffect(() => {
    const middleware = bearerToken(session.getAccessToken.bind(session))
    api.use(middleware)
    return () => api.eject(middleware)
  }, [session])

  const openSignIn = useCallback((returnTo?: string) => {
    setSignInError(null)
    setDialog({ open: true, returnTo: returnTo ?? currentPath() })
  }, [])
  const closeSignIn = useCallback(() => setDialog((current) => ({ ...current, open: false })), [])

  const continueWithGoogle = useCallback(
    async (returnTo?: string) => {
      setSignInError(null)
      try {
        navigate(await buildGoogleSignInUrl(returnTo ?? currentPath()))
      } catch (error) {
        setSignInError(
          error instanceof ApiError && error.status === 503 ? 'notSetUp' : 'unreachable',
        )
      }
    },
    [navigate],
  )

  const value = useMemo<AuthContextValue>(
    () => ({ state, session, dialog, openSignIn, closeSignIn, continueWithGoogle, signInError }),
    [state, session, dialog, openSignIn, closeSignIn, continueWithGoogle, signInError],
  )
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
