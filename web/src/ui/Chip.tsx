import type { ReactNode } from 'react'
import { Icon } from '../components/icons'

interface ChipProps {
  children: ReactNode
  /** On: filled teal (an active filter); off: outlined. */
  selected?: boolean
  onClick?: () => void
  /** Shows a × that removes the chip (a filter that is set), with this label for screen readers. */
  onRemove?: () => void
  removeLabel?: string
}

/** A filter chip (B1, B3, C1, E1…): a toggle, or a set filter with a × to clear it. */
export function Chip({ children, selected = false, onClick, onRemove, removeLabel }: ChipProps) {
  if (onRemove) {
    return (
      <span className="ui-chip ui-chip-set">
        {children}
        <button
          type="button"
          className="ui-chip-remove"
          aria-label={removeLabel ?? `Remove ${String(children)}`}
          onClick={onRemove}
        >
          <Icon name="close" size={16} strokeWidth={2.6} />
        </button>
      </span>
    )
  }
  return (
    <button type="button" className="ui-chip" aria-pressed={selected} onClick={onClick}>
      {children}
    </button>
  )
}
