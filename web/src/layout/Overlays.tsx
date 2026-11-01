import { useEffect, useRef, type ReactNode } from 'react'
import { useAuth } from '../auth/authContext'
import { Icon } from '../components/icons'
import { SignedOutPrompt } from '../components/SignedOutPrompt'
import { ComingSoon } from '../screens/ComingSoon'
import { useOverlay, type OverlayName } from './overlay'

function Panel({
  name,
  title,
  onClose,
  children,
}: {
  name: OverlayName
  title: string
  onClose: () => void
  children: ReactNode
}) {
  const panel = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null
    panel.current?.focus()
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose()
      }
    }
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('keydown', onKey)
      previous?.focus?.()
    }
  }, [onClose])

  return (
    <div className="overlay" data-overlay={name} onClick={onClose}>
      <div
        ref={panel}
        className="overlay-panel"
        role="dialog"
        aria-modal="true"
        aria-labelledby={`overlay-${name}-title`}
        tabIndex={-1}
        onClick={(event) => event.stopPropagation()}
      >
        <header className="overlay-head">
          <h2 id={`overlay-${name}-title`}>{title}</h2>
          <button type="button" className="icon-button" aria-label="Close" onClick={onClose}>
            <Icon name="close" size={20} />
          </button>
        </header>
        <div className="overlay-body">{children}</div>
      </div>
    </div>
  )
}

/**
 * Search (H2: full screen on the phone, a window on the desktop) and the AI chat (H1: full screen on the phone, a
 * side panel on the desktop). The frames work now; what's inside comes with E12-F01 and E11-F01.
 */
export function Overlays() {
  const { current, close } = useOverlay()
  const { state } = useAuth()

  if (current === 'search') {
    return (
      <Panel name="search" title="Search" onClose={close}>
        <ComingSoon
          screen="H2"
          name="Search"
          feature="E12-F01 Search everywhere"
          text="Find any Pokémon, move, raid boss or event from here."
        />
      </Panel>
    )
  }
  if (current === 'ask') {
    return (
      <Panel name="ask" title="Ask" onClose={close}>
        {state.status === 'signedOut' ? (
          <SignedOutPrompt feature="ai" />
        ) : (
          <ComingSoon
            screen="H1"
            name="AI chat"
            feature="E11-F01 Chat that answers from our data"
            text="Ask anything about Pokémon GO, answered from the app’s own data."
          />
        )}
      </Panel>
    )
  }
  return null
}
