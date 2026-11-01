import { Icon } from '../components/icons'
import { useOverlay } from './overlay'

/** "Ask": opens the AI chat (H1) over the page. On every screen's header and the desktop top bar. */
export function AskButton() {
  const { open } = useOverlay()
  return (
    <button type="button" className="ask-button" onClick={() => open('ask')}>
      <Icon name="ask" size={18} />
      Ask
    </button>
  )
}
