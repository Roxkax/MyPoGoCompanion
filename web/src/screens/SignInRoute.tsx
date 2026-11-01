import { useEffect, useRef } from 'react'
import { Navigate, useNavigate } from 'react-router'
import { useAuth } from '../auth/authContext'
import { TodayScreen } from './TodayScreen'

/**
 * /sign-in: the sign-in screen (phone) or window (desktop) over Today. After Google the person lands on Today;
 * closing it ("Not now", Back, Escape) leaves the address for Today too.
 */
export function SignInRoute() {
  const { state, dialog, openSignIn } = useAuth()
  const navigate = useNavigate()
  const opened = useRef(false)
  const shown = useRef(false)

  useEffect(() => {
    if (state.status === 'signedOut' && !opened.current) {
      opened.current = true
      openSignIn('/')
    }
  }, [state.status, openSignIn])

  useEffect(() => {
    if (dialog.open) {
      shown.current = true
    } else if (shown.current) {
      navigate('/', { replace: true })
    }
  }, [dialog.open, navigate])

  if (state.status === 'signedIn') {
    return <Navigate to="/" replace />
  }
  return <TodayScreen />
}
