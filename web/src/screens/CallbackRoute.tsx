import { useCallback } from 'react'
import { useNavigate } from 'react-router'
import { AuthCallback } from '../components/AuthCallback'

/** /auth/callback: Google sends people back here; the sign-in finishes and they return to where they started. */
export function CallbackRoute() {
  const navigate = useNavigate()
  const leave = useCallback((to: string) => navigate(to, { replace: true }), [navigate])
  return (
    <main className="screen">
      <AuthCallback onDone={leave} />
    </main>
  )
}
