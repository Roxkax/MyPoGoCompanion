import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../auth/authContext'
import { completeGoogleSignIn, SignInError } from '../auth/google'
import { useCurrentLanguage } from '../i18n/languageContext'

/**
 * The page Google sends people back to (/auth/callback): swaps the code for a session, then returns to the page they
 * started from. On failure it says why and offers to try again.
 */
export function AuthCallback({ onDone }: { onDone: (path: string) => void }) {
  const { session, openSignIn } = useAuth()
  const [error, setError] = useState<'cancelled' | 'expired' | 'notConfirmed' | null>(null)
  const started = useRef(false)
  const { t } = useTranslation()
  const language = useCurrentLanguage()

  useEffect(() => {
    // Once only: React's development double-run would use the one-time code twice.
    if (started.current) {
      return
    }
    started.current = true
    completeGoogleSignIn(window.location.search, window.location.origin, language)
      .then(({ session: signedIn, returnTo }) => {
        session.accept(signedIn)
        onDone(returnTo)
      })
      .catch((reason: unknown) => {
        void session.start()
        setError(reason instanceof SignInError ? reason.reason : 'notConfirmed')
      })
  }, [session, onDone, language])

  if (!error) {
    return (
      <p className="callback" role="status">
        {t('signIn.signingIn')}
      </p>
    )
  }

  return (
    <div className="callback card">
      <p role="alert">{t(`signIn.errors.${error}`)}</p>
      <div className="callback-actions">
        <button
          type="button"
          className="button"
          onClick={() => {
            onDone('/')
            openSignIn('/')
          }}
        >
          {t('signIn.tryAgain')}
        </button>
        <button type="button" className="link-button" onClick={() => onDone('/')}>
          {t('signIn.backToApp')}
        </button>
      </div>
    </div>
  )
}
