import { useTranslation } from 'react-i18next'
import type { AppEnvironment } from '../environment'

/** A strip across the top on QA and local copies of the app; nothing in production. */
export function EnvironmentBanner({ environment }: { environment: AppEnvironment }) {
  const { t } = useTranslation()
  if (environment.kind === 'production') {
    return null
  }

  return (
    <div
      className="env-banner"
      data-env={environment.kind}
      role="note"
      aria-label={t('environment.label', { name: environment.label })}
    >
      <strong className="env-banner-label">{environment.label}</strong>
      <span>{t(`environment.${environment.kind}`)}</span>
      {environment.detail && <span className="env-banner-detail num">{environment.detail}</span>}
    </div>
  )
}
