import { useTranslation } from 'react-i18next'
import { ErrorReference } from '../components/ErrorReference'
import { Icon } from '../components/icons'
import { useConnection } from '../pwa/pwaContext'

/**
 * Shown in place of the page when the internet works but the API doesn't answer (E01-F08). The app reloads by
 * itself when the API is back; Try again checks at once. A reference shows when the failed answer had one.
 */
export function ApiDownScreen() {
  const { checking, stillFailing, reference, tryAgain } = useConnection()
  const { t } = useTranslation()
  return (
    <main className="screen offline-screen api-down-screen">
      <span className="offline-icon">
        <Icon name="serverDown" size={44} strokeWidth={1.8} />
      </span>
      <h1>{t('apiDown.title')}</h1>
      <p>{t('apiDown.text')}</p>
      <button type="button" className="button" disabled={checking} onClick={() => void tryAgain()}>
        {checking ? t('offline.checking') : t('offline.tryAgain')}
      </button>
      <p className="offline-status" role="status">
        {stillFailing && !checking ? t('apiDown.stillDown') : ''}
      </p>
      <ErrorReference reference={reference} />
    </main>
  )
}
