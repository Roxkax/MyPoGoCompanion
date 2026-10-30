import { useQuery } from '@tanstack/react-query'
import { fetchHealth } from '../api/health'

/** Shows whether the web app can reach the API and whether the API can reach its database. */
export function ApiStatus() {
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
    title = 'Checking the API…'
  } else if (health.isError) {
    tone = 'danger'
    title = 'Can’t reach the API'
    detail = 'Check your connection and try again.'
  } else if (health.data.status === 'Unhealthy') {
    tone = 'warn'
    title = 'The API is running, but its database isn’t answering'
    detail = `Version ${health.data.version}`
  } else {
    tone = 'ok'
    title = 'Connected to the API'
    detail = `Version ${health.data.version} · database working`
  }

  return (
    <section className="card api-status" data-tone={tone} aria-labelledby="api-status-title">
      <h2 id="api-status-title" className="card-label">
        API status
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
          {health.isFetching ? 'Trying…' : 'Try again'}
        </button>
      )}
    </section>
  )
}
