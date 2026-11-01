import type { AppEnvironment } from '../environment'

const explanation: Record<'qa' | 'local', string> = {
  qa: 'Test version, not the real app',
  local: 'Running on this computer',
}

/** A strip across the top on QA and local copies of the app; nothing in production. */
export function EnvironmentBanner({ environment }: { environment: AppEnvironment }) {
  if (environment.kind === 'production') {
    return null
  }

  return (
    <div
      className="env-banner"
      data-env={environment.kind}
      role="note"
      aria-label={`${environment.label} environment`}
    >
      <strong className="env-banner-label">{environment.label}</strong>
      <span>{explanation[environment.kind]}</span>
      {environment.detail && <span className="env-banner-detail num">{environment.detail}</span>}
    </div>
  )
}
