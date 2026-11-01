import { useTranslation } from 'react-i18next'
import { Icon } from '../components/icons'
import { useConnection } from '../pwa/pwaContext'

/**
 * H3: shown in place of the page while the device has no connection (E01-F06). The app reloads by itself when the
 * connection is back; Try again checks at once.
 */
export function OfflineScreen() {
  const { checking, stillOffline, tryAgain } = useConnection()
  const { t } = useTranslation()
  return (
    <main className="screen offline-screen">
      <span className="offline-icon">
        <Icon name="offline" size={44} strokeWidth={1.8} />
      </span>
      <h1>{t('offline.title')}</h1>
      <p>{t('offline.text')}</p>
      <button type="button" className="button" disabled={checking} onClick={() => void tryAgain()}>
        {checking ? t('offline.checking') : t('offline.tryAgain')}
      </button>
      <p className="offline-status" role="status">
        {stillOffline && !checking ? t('offline.stillOffline') : ''}
      </p>
      <p className="fine-print">{t('offline.later')}</p>
    </main>
  )
}
