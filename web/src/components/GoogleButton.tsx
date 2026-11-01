import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../auth/authContext'
import { GoogleLogo } from './icons'

/** "Continue with Google": white with Google's G in light and dark mode (Google's branding rules). */
export function GoogleButton({ returnTo }: { returnTo?: string }) {
  const { continueWithGoogle } = useAuth()
  const [leaving, setLeaving] = useState(false)
  const { t } = useTranslation()

  async function onClick() {
    setLeaving(true)
    await continueWithGoogle(returnTo)
    // Only reached when leaving for Google failed; the error is shown by the caller.
    setLeaving(false)
  }

  return (
    <button type="button" className="google-button" onClick={onClick} disabled={leaving}>
      <GoogleLogo />
      {t('signIn.google')}
    </button>
  )
}
