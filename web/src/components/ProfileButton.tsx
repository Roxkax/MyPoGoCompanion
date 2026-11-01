import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router'
import type { Role } from '../auth/authApi'
import { useAuth } from '../auth/authContext'
import { isAdmin } from '../auth/roles'
import { Icon } from './icons'

const roleBadge: Record<Role, string | null> = {
  User: null,
  Admin: 'Admin',
  SuperAdmin: 'Super admin',
}

/**
 * The profile button at the top right (app map: Sign in, Settings, Admin for admins). Signed out, its menu offers
 * "Sign in" and "Settings"; signed in it shows your picture, and its menu your account and role, Settings, Admin (for
 * admins), "Sign out" (this device) and "Sign out everywhere" (every device).
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

  const closeMenu = () => setMenuOpen(false)
  const settings = (
    <Link to="/settings" role="menuitem" onClick={closeMenu}>
      Settings
    </Link>
  )

  if (state.status !== 'signedIn') {
    return (
      <div className="profile" ref={menu}>
        <button
          type="button"
          className="avatar"
          aria-label="Profile"
          aria-haspopup="menu"
          aria-expanded={menuOpen}
          onClick={() => setMenuOpen((open) => !open)}
          disabled={state.status === 'loading'}
        >
          <Icon name="person" size={20} />
        </button>
        {menuOpen && (
          <div className="profile-menu" role="menu" aria-label="Profile">
            <button
              type="button"
              role="menuitem"
              onClick={() => {
                closeMenu()
                openSignIn()
              }}
            >
              Sign in
            </button>
            {settings}
          </div>
        )}
      </div>
    )
  }

  const { user } = state
  const badge = roleBadge[user.role]

  async function everywhere() {
    setError(null)
    try {
      await session.signOutEverywhere()
      closeMenu()
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
          <span aria-hidden="true" className="avatar-letter">
            {user.name.slice(0, 1).toUpperCase()}
          </span>
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
          {settings}
          {isAdmin(user.role) && (
            <Link to="/admin" role="menuitem" onClick={closeMenu}>
              Admin
            </Link>
          )}
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
