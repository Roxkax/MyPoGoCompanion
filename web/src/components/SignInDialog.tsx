import { useEffect, useRef } from 'react'
import { useAuth } from '../auth/authContext'
import { GoogleButton } from './GoogleButton'
import { AppLogo, Icon, type IconName } from './icons'

const perks: { icon: IconName; text: string }[] = [
  { icon: 'collection', text: 'Your collection, uploaded from Poke Genie' },
  { icon: 'battle', text: '"Your counters" and "your team" everywhere' },
  { icon: 'filters', text: 'Saved filters on every device' },
  { icon: 'ask', text: 'Ask the AI assistant' },
]

/**
 * Screen F1: what signing in adds, "Continue with Google" and "Not now". Full screen with a Back link on a phone, a
 * window over the page on a desktop. After Google, the person comes back to the page they opened it from.
 */
export function SignInDialog() {
  const { dialog, closeSignIn, signInError, state } = useAuth()
  const panel = useRef<HTMLDivElement>(null)
  const open = dialog.open && state.status !== 'signedIn'

  useEffect(() => {
    if (!open) {
      return
    }
    const previous = document.activeElement as HTMLElement | null
    panel.current?.focus()
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        closeSignIn()
      }
    }
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('keydown', onKey)
      previous?.focus?.()
    }
  }, [open, closeSignIn])

  if (!open) {
    return null
  }

  return (
    <div className="sign-in-overlay" onClick={closeSignIn}>
      <div
        ref={panel}
        className="sign-in"
        role="dialog"
        aria-modal="true"
        aria-labelledby="sign-in-title"
        tabIndex={-1}
        onClick={(event) => event.stopPropagation()}
      >
        <header className="sign-in-back">
          <button type="button" className="link-button" onClick={closeSignIn}>
            ‹ Back
          </button>
        </header>
        <div className="sign-in-body">
          <AppLogo />
          <h2 id="sign-in-title">Sign in to make it yours</h2>
          <p className="sign-in-lead">
            Everything else works without an account: Pokédex, raids, tier lists, filters and the
            calendar.
          </p>
          <ul className="perks">
            {perks.map((perk) => (
              <li key={perk.icon}>
                <span className="perk-icon">
                  <Icon name={perk.icon} size={20} />
                </span>
                {perk.text}
              </li>
            ))}
          </ul>
          <GoogleButton returnTo={dialog.returnTo} />
          {signInError && (
            <p className="sign-in-error" role="alert">
              {signInError}
            </p>
          )}
          <button type="button" className="link-button" onClick={closeSignIn}>
            Not now, keep browsing
          </button>
          <p className="fine-print">
            We keep only your Google name, email and picture, your settings and what you upload. No
            passwords. MyPoGoCompanion is a fan project, not affiliated with Niantic or The Pokémon
            Company.
          </p>
        </div>
      </div>
    </div>
  )
}
