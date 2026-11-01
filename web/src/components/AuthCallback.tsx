import { useEffect, useRef, useState } from 'react'
import { useAuth } from '../auth/authContext'
import { completeGoogleSignIn, SignInError } from '../auth/google'

/**
 * The page Google sends people back to (/auth/callback): swaps the code for a session, then returns to the page they
 * started from. On failure it says why and offers to try again.
 */
export function AuthCallback({ onDone }: { onDone: (path: string) => void }) {
  const { session, openSignIn } = useAuth()
  const [error, setError] = useState<string | null>(null)
  const started = useRef(false)

  useEffect(() => {
    // Once only: React's development double-run would use the one-time code twice.
    if (started.current) {
      return
    }
    started.current = true
    completeGoogleSignIn(window.location.search)
      .then(({ session: signedIn, returnTo }) => {
        session.accept(signedIn)
        onDone(returnTo)
      })
      .catch((reason: unknown) => {
        void session.start()
        setError(
          reason instanceof SignInError
            ? reason.message
            : "Google didn't confirm the sign-in. Please try again.",
        )
      })
  }, [session, onDone])

  if (!error) {
    return (
      <p className="callback" role="status">
        Signing you in…
      </p>
    )
  }

  return (
    <div className="callback card">
      <p role="alert">{error}</p>
      <div className="callback-actions">
        <button
          type="button"
          className="button"
          onClick={() => {
            onDone('/')
            openSignIn()
          }}
        >
          Try again
        </button>
        <button type="button" className="link-button" onClick={() => onDone('/')}>
          Back to the app
        </button>
      </div>
    </div>
  )
}
