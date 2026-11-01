import { useTranslation } from 'react-i18next'
import { Icon } from '../components/icons'
import { useConnection, useInstallHint } from './pwaContext'
import { useInstallAction } from './useInstallAction'

/**
 * The one-time install hint (decision 2026-11-01): a small banner above the phone's tab bar from the second visit,
 * where the app can really be installed. Install or ✕ hides it for good on this device.
 */
export function InstallHint({ icon }: { icon: string }) {
  const { kind, install, guide } = useInstallAction()
  const { shows, dismiss } = useInstallHint(kind)
  const { offline } = useConnection()
  const { t } = useTranslation()

  return (
    <>
      {shows && !offline && (
        <aside className="install-hint" aria-labelledby="install-hint-title">
          <img src={icon} alt="" width={36} height={36} />
          <div className="install-hint-text">
            <strong id="install-hint-title">{t('install.hintTitle')}</strong>
            <span>{t('install.hintText')}</span>
          </div>
          <button
            type="button"
            className="button install-hint-button"
            onClick={() => {
              dismiss()
              void install()
            }}
          >
            {t('install.hintInstall')}
          </button>
          <button
            type="button"
            className="icon-button"
            aria-label={t('install.hintClose')}
            onClick={dismiss}
          >
            <Icon name="close" size={18} />
          </button>
        </aside>
      )}
      {guide}
    </>
  )
}
