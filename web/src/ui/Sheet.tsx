import { useEffect, useEffectEvent, useRef, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '../components/icons'

/**
 * A sheet over the page: from the bottom on the phone, a small window on the desktop. Escape, the ✕ and a tap
 * outside close it; focus moves into it and back to where it was.
 */
export function Sheet({
  id,
  title,
  onClose,
  headerEnd,
  footer,
  wide = false,
  children,
}: {
  /** Names the title for screen readers (`<id>-title`). */
  id: string
  title: string
  onClose: () => void
  /** Next to the title, before the ✕ (e.g. "Clear all"). */
  headerEnd?: ReactNode
  /** Stays at the bottom while the content scrolls (e.g. Reset · Show 7 Pokémon). */
  footer?: ReactNode
  /** A wider window on the desktop. */
  wide?: boolean
  children: ReactNode
}) {
  const panel = useRef<HTMLDivElement>(null)
  const { t } = useTranslation()
  const close = useEffectEvent(() => onClose())

  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null
    panel.current?.focus()
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') close()
    }
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('keydown', onKey)
      previous?.focus?.()
    }
  }, [])

  return (
    <div className="sheet-overlay" onClick={onClose}>
      <div
        ref={panel}
        className="sheet ui-sheet"
        data-wide={wide || undefined}
        role="dialog"
        aria-modal="true"
        aria-labelledby={`${id}-title`}
        tabIndex={-1}
        onClick={(event) => event.stopPropagation()}
      >
        <header className="sheet-head">
          <h2 id={`${id}-title`}>{title}</h2>
          {headerEnd}
          <button
            type="button"
            className="icon-button"
            aria-label={t('header.close')}
            onClick={onClose}
          >
            <Icon name="close" size={20} />
          </button>
        </header>
        <div className="ui-sheet-body">{children}</div>
        {footer && <footer className="ui-sheet-foot">{footer}</footer>}
      </div>
    </div>
  )
}
