import { useEffect, useRef, useState } from 'react'
import type { Role } from '../auth/authApi'
import { useAuth } from '../auth/authContext'
import { Icon } from './icons'

const roleBadge: Record<Role, string | null> = {
  User: null,
  Admin: 'Admin',
  SuperAdmin: 'Super admin',
}

/**
 * The profile button at the top right. Signed out it opens the sign-in screen; signed in it shows your picture and a
 * menu with your account, role, "Sign out" (this device) and "Sign out everywhere" (every device).
 */
export function ProfileButton() {
  const { state, openSignIn, session } = useAuth()
  const [menuOpen, setMenuOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const menu = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!menuOpen) {
      return
    }
    const close = (event: MouseEvent | KeyboardEvent) => {
      if (
        event instanceof KeyboardEvent
          ? event.key === 'Escape'
          : !menu.current?.contains(event.target as Node)
      ) {
        setMenuOpen(false)
      }
    }
    document.addEventListener('mousedown', close)
    document.addEventListener('keydown', close)
    return () => {
      document.removeEventListener('mousedown', close)
      document.removeEventListener('keydown', close)
    }
  }, [menuOpen])

  if (state.status !== 'signedIn') {
    return (
      <button
        type="button"
        className="avatar"
        aria-label="Sign in"
        onClick={openSignIn}
        disabled={state.status === 'loading'}
      >
        <Icon name="person" size={20} />
      </button>
    )
  }

  const { user } = state
  const badge = roleBadge[user.role]

  async function everywhere() {
    setError(null)
    try {
      await session.signOutEverywhere()
      setMenuOpen(false)
    } catch {
      setError("Couldn't reach the server. Try again.")
    }
  }

  return (
    <div className="profile" ref={menu}>
      <button
        type="button"
        className="avatar"
        aria-label={`Account: ${user.name}`}
        aria-haspopup="menu"
        aria-expanded={menuOpen}
        onClick={() => setMenuOpen((open) => !open)}
      >
        {user.pictureUrl ? (
          <img src={user.pictureUrl} alt="" referrerPolicy="no-referrer" />
        ) : (
          <span aria-hidden="true">{user.name.slice(0, 1).toUpperCase()}</span>
        )}
      </button>
      {menuOpen && (
        <div className="profile-menu" role="menu" aria-label="Account">
          <div className="profile-who">
            <strong>{user.name}</strong>
            <span>{user.email}</span>
            {badge && (
              <span className="role-badge">
                <Icon name="admin" size={14} />
                {badge}
              </span>
            )}
          </div>
          <button type="button" role="menuitem" onClick={() => void session.signOut()}>
            Sign out
          </button>
          <button type="button" role="menuitem" onClick={() => void everywhere()}>
            Sign out everywhere
          </button>
          {error && (
            <p className="sign-in-error" role="alert">
              {error}
            </p>
          )}
        </div>
      )}
    </div>
  )
}
