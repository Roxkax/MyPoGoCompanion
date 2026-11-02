import { useTranslation } from 'react-i18next'
import { ApiError } from '../api/client'
import { Card } from '../ui/Card'
import { ErrorReference } from './ErrorReference'

/**
 * In place of a screen's data when the API answered with an error (E01-F08): what happened, Try again and the
 * reference. No answer at all is not this: the offline or "isn't answering" screen takes over then.
 */
export function LoadError({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const { t } = useTranslation()
  const reference = error instanceof ApiError ? error.reference : null
  return (
    <Card as="section" className="load-error" role="alert" aria-labelledby="load-error-title">
      <h2 id="load-error-title">{t('loadError.title')}</h2>
      <p>{t('loadError.text')}</p>
      <button type="button" className="button" onClick={onRetry}>
        {t('loadError.tryAgain')}
      </button>
      <ErrorReference reference={reference} />
    </Card>
  )
}
