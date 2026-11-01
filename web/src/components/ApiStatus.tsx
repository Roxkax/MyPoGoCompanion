import { useTranslation } from 'react-i18next'
import { $api } from '../api/client'
import { isHealthReport } from '../api/health'

/** Shows whether the web app can reach the API and whether the API can reach its database. */
export function ApiStatus() {
  const { t } = useTranslation()
  const health = $api.useQuery('get', '/health', undefined, {
    // One more try when the API can't be reached; a report saying the database is down is an answer, not a failure.
    retry: (failures, error) => failures < 1 && !isHealthReport(error),
  })
  const report = health.data ?? (isHealthReport(health.error) ? health.error : undefined)

  let tone: 'ok' | 'warn' | 'danger' | 'pending'
  let title: string
  let detail: string | null = null

  if (health.isPending) {
    tone = 'pending'
    title = t('apiStatus.checking')
  } else if (!report) {
    tone = 'danger'
    title = t('apiStatus.unreachable')
    detail = t('apiStatus.unreachableDetail')
  } else if (report.status === 'Unhealthy') {
    tone = 'warn'
    title = t('apiStatus.noDatabase')
    detail = t('apiStatus.version', { version: report.version })
  } else {
    tone = 'ok'
    title = t('apiStatus.connected')
    detail = t('apiStatus.versionWorking', { version: report.version })
  }

  return (
    <section className="card api-status" data-tone={tone} aria-labelledby="api-status-title">
      <h2 id="api-status-title" className="card-label">
        {t('apiStatus.title')}
      </h2>
      <div role="status" aria-live="polite" className="api-status-body">
        <span className="dot" aria-hidden="true" />
        <div>
          <p className="api-status-title">{title}</p>
          {detail && <p className="api-status-detail num">{detail}</p>}
        </div>
      </div>
      {(tone === 'danger' || tone === 'warn') && (
        <button
          type="button"
          className="button"
          onClick={() => void health.refetch()}
          disabled={health.isFetching}
        >
          {health.isFetching ? t('apiStatus.trying') : t('apiStatus.tryAgain')}
        </button>
      )}
    </section>
  )
}
