import { useEffect, useId, useRef, useState, type ReactNode } from 'react'

/**
 * A chip that opens a small panel under it (the desktop Pokédex's Type ▾ and Generation ▾). Changes in the panel apply
 * at once; a click outside it, Escape or the chip again closes it, and Escape brings the focus back to the chip.
 */
export function Popover({
  label,
  title,
  active = false,
  children,
}: {
  /** The chip's text ("Type: Any ▾"). */
  label: ReactNode
  /** Names the panel for screen readers ("Type"). */
  title: string
  /** Something is chosen: the chip shows as set. */
  active?: boolean
  children: ReactNode
}) {
  const [open, setOpen] = useState(false)
  const root = useRef<HTMLSpanElement>(null)
  const button = useRef<HTMLButtonElement>(null)
  const id = useId()

  useEffect(() => {
    if (!open) return
    const onPointer = (event: MouseEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false)
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
        button.current?.focus()
      }
    }
    document.addEventListener('mousedown', onPointer)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onPointer)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  return (
    <span className="ui-popover" ref={root}>
      <button
        ref={button}
        type="button"
        className="ui-chip"
        data-active={active || undefined}
        aria-expanded={open}
        aria-controls={id}
        aria-haspopup="dialog"
        onClick={() => setOpen(!open)}
      >
        {label}
      </button>
      {open && (
        <div id={id} className="ui-popover-panel" role="dialog" aria-label={title}>
          {children}
        </div>
      )}
    </span>
  )
}
