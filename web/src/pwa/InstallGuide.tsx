import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '../components/icons'

/**
 * Where a browser without its own install window keeps "install": Share → Add to Home Screen on iPhone and iPad,
 * the browser's menu elsewhere. A bottom sheet on the phone, a small window on the desktop.
 */
export function InstallGuide({ kind, onClose }: { kind: 'ios' | 'menu'; onClose: () => void }) {
  const panel = useRef<HTMLDivElement>(null)
  const { t } = useTranslation()

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
    <div className="sheet-overlay" onClick={onClose}>
      <div
        ref={panel}
        className="sheet"
        role="dialog"
        aria-modal="true"
        aria-labelledby="install-guide-title"
        tabIndex={-1}
        onClick={(event) => event.stopPropagation()}
      >
        <header className="sheet-head">
          <h2 id="install-guide-title">{t('install.guideTitle')}</h2>
          <button
            type="button"
            className="icon-button"
            aria-label={t('header.close')}
            onClick={onClose}
          >
            <Icon name="close" size={20} />
          </button>
        </header>
        {kind === 'ios' ? (
          <ol className="install-steps">
            <li>
              <span className="install-step-icon">
                <Icon name="share" size={20} />
              </span>
              {t('install.iosStep1')}
            </li>
            <li>
              <span className="install-step-icon">
                <Icon name="addBox" size={20} />
              </span>
              {t('install.iosStep2')}
            </li>
            <li>
              <span className="install-step-icon">
                <Icon name="check" size={20} />
              </span>
              {t('install.iosStep3')}
            </li>
          </ol>
        ) : (
          <div className="install-menu">
            <p>{t('install.menuText')}</p>
            <p>{t('install.menuMac')}</p>
            <p>{t('install.menuFirefox')}</p>
          </div>
        )}
        <button type="button" className="button sheet-done" onClick={onClose}>
          {t('install.done')}
        </button>
      </div>
    </div>
  )
}
