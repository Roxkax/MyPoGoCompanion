import { useEffect } from 'react'
import { useAuth } from '../auth/authContext'
import { isLanguage } from './languages'
import { useLanguage } from './languageContext'

/**
 * The account's language wins (decision 2026-11-01): whenever the API hands over the signed-in person (sign-in,
 * and each refresh, so also every time the app opens), this device switches to the account's language and keeps it.
 */
export function AccountLanguage() {
  const { state } = useAuth()
  const { setLanguage } = useLanguage()
  const user = state.status === 'signedIn' ? state.user : null

  useEffect(() => {
    if (user && isLanguage(user.language)) {
      setLanguage(user.language)
    }
  }, [user, setLanguage])

  return null
}
