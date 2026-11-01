import { Icon } from '../components/icons'
import { useOverlay } from './overlay'

/** Looks like a search box; opens search (H2) over the page. Ctrl K (⌘ K) also opens it. */
export function SearchButton({ wide = false }: { wide?: boolean }) {
  const { open } = useOverlay()
  return (
    <button
      type="button"
      className="search-button"
      data-wide={wide || undefined}
      onClick={() => open('search')}
      aria-keyshortcuts="Control+K Meta+K"
    >
      <Icon name="search" size={19} />
      <span className="search-button-text">
        {wide ? 'Search Pokémon, moves, bosses, events' : 'Search Pokémon, moves, bosses…'}
      </span>
      {wide && <kbd>Ctrl K</kbd>}
    </button>
  )
}
