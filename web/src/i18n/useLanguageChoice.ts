import { useCallback, useState } from 'react'
import { saveLanguage } from '../auth/authApi'
import { useAuth } from '../auth/authContext'
import type { Language } from './languages'
import { useLanguage } from './languageContext'

/**
 * Settings → Language: shows the choice at once and saves it on this device; signed in, also with the account, so
 * every device follows. `notSaved` is true when the account couldn't be reached (this device keeps the choice).
 */
export function useLanguageChoice() {
  const { language, setLanguage } = useLanguage()
  const { state, session } = useAuth()
  const [notSaved, setNotSaved] = useState(false)
  const signedIn = state.status === 'signedIn'

  const choose = useCallback(
    async (next: Language) => {
      setNotSaved(false)
      setLanguage(next)
      if (!signedIn) {
        return
      }
      try {
        const token = await session.getAccessToken()
        if (!token) {
          return
        }
        await saveLanguage(token, next)
        session.updateUser({ language: next })
      } catch {
        setNotSaved(true)
      }
    },
    [setLanguage, signedIn, session],
  )

  return { language, choose, notSaved }
}
