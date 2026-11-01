import { useTranslation } from 'react-i18next'
import { Icon } from '../components/icons'
import { useIsDesktop } from '../layout/useMediaQuery'
import { useInstallAction } from './useInstallAction'

/** Settings → More → Install the app (F2): hidden once the app is installed. */
export function InstallRow() {
  const { kind, install, guide } = useInstallAction()
  const desktop = useIsDesktop()
  const { t } = useTranslation()

  if (kind === 'installed') {
    return null
  }
  return (
    <section className="settings-group" aria-labelledby="settings-more">
      <h2 id="settings-more" className="group-label">
        {t('install.more')}
      </h2>
      <div className="ui-card settings-rows">
        <button type="button" className="settings-row" onClick={() => void install()}>
          <span>{t('install.row')}</span>
          <span className="settings-row-value">
            {t(desktop ? 'install.rowDesktop' : 'install.rowPhone')}
            <Icon name="chevron" size={18} strokeWidth={2.4} />
          </span>
        </button>
      </div>
      {guide}
    </section>
  )
}
