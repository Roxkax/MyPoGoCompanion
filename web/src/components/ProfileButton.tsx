import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { Role } from '../auth/authApi'
import { useAuth } from '../auth/authContext'
import { isAdmin } from '../auth/roles'
import { Icon } from './icons'

const roleBadge = {
  User: null,
  Admin: 'profile.admin',
  SuperAdmin: 'profile.superAdmin',
} as const satisfies Record<Role, string | null>

/**
 * The profile button at the top right (app map: Sign in, Settings, Admin for admins). Signed out, its menu offers
 * "Sign in" and "Settings"; signed in it shows your picture, and its menu your account and role, Settings, Admin (for
 * admins), "Sign out" (this device) and "Sign out everywhere" (every device).
 */
export function ProfileButton() {
  const { state, openSignIn, session } = useAuth()
  const [menuOpen, setMenuOpen] = useState(false)
  const [error, setError] = useState(false)
  const { t } = useTranslation()
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
      {t('nav.settings')}
    </Link>
  )

  if (state.status !== 'signedIn') {
    return (
      <div className="profile" ref={menu}>
        <button
          type="button"
          className="avatar"
          aria-label={t('profile.profile')}
          aria-haspopup="menu"
          aria-expanded={menuOpen}
          onClick={() => setMenuOpen((open) => !open)}
          disabled={state.status === 'loading'}
        >
          <Icon name="person" size={20} />
        </button>
        {menuOpen && (
          <div className="profile-menu" role="menu" aria-label={t('profile.profile')}>
            <button
              type="button"
              role="menuitem"
              onClick={() => {
                closeMenu()
                openSignIn()
              }}
            >
              {t('profile.signIn')}
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
    setError(false)
    try {
      await session.signOutEverywhere()
      closeMenu()
    } catch {
      setError(true)
    }
  }

  return (
    <div className="profile" ref={menu}>
      <button
        type="button"
        className="avatar"
        aria-label={t('profile.accountOf', { name: user.name })}
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
        <div className="profile-menu" role="menu" aria-label={t('profile.account')}>
          <div className="profile-who">
            <strong>{user.name}</strong>
            <span>{user.email}</span>
            {badge && (
              <span className="role-badge">
                <Icon name="admin" size={14} />
                {t(badge)}
              </span>
            )}
          </div>
          {settings}
          {isAdmin(user.role) && (
            <Link to="/admin" role="menuitem" onClick={closeMenu}>
              {t('nav.admin')}
            </Link>
          )}
          <button type="button" role="menuitem" onClick={() => void session.signOut()}>
            {t('profile.signOut')}
          </button>
          <button type="button" role="menuitem" onClick={() => void everywhere()}>
            {t('profile.signOutEverywhere')}
          </button>
          {error && (
            <p className="sign-in-error" role="alert">
              {t('profile.unreachable')}
            </p>
          )}
        </div>
      )}
    </div>
  )
}
