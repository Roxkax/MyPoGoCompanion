import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { fetchHealth } from '../api/health'

/** Shows whether the web app can reach the API and whether the API can reach its database. */
export function ApiStatus() {
  const { t } = useTranslation()
  const health = useQuery({
    queryKey: ['health'],
    queryFn: ({ signal }) => fetchHealth(signal),
    retry: 1,
  })

  let tone: 'ok' | 'warn' | 'danger' | 'pending'
  let title: string
  let detail: string | null = null

  if (health.isPending) {
    tone = 'pending'
    title = t('apiStatus.checking')
  } else if (health.isError) {
    tone = 'danger'
    title = t('apiStatus.unreachable')
    detail = t('apiStatus.unreachableDetail')
  } else if (health.data.status === 'Unhealthy') {
    tone = 'warn'
    title = t('apiStatus.noDatabase')
    detail = t('apiStatus.version', { version: health.data.version })
  } else {
    tone = 'ok'
    title = t('apiStatus.connected')
    detail = t('apiStatus.versionWorking', { version: health.data.version })
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
