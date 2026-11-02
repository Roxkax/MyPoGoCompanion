import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useRouteError } from 'react-router'
import { ErrorReference } from '../components/ErrorReference'
import { newReference } from '../errors/reference'
import { useErrorReporter } from '../errors/reporterContext'
import { Card } from '../ui/Card'

/**
 * Shown when a screen breaks while drawing (E01-F08): a way out instead of a blank page, inside the normal frame.
 * The error is reported once, with the reference shown here.
 */
export function RouteError() {
  const error = useRouteError()
  const reporter = useErrorReporter()
  const { t } = useTranslation()
  const [reference] = useState(() => newReference())

  useEffect(() => {
    reporter.report('Screen', error, reference)
  }, [reporter, error, reference])

  return (
    <main className="screen route-error">
      <Card as="section" aria-labelledby="route-error-title">
        <h1 id="route-error-title">{t('routeError.title')}</h1>
        <p>{t('routeError.text')}</p>
        <div className="error-actions">
          <button type="button" className="button" onClick={() => window.location.reload()}>
            {t('routeError.reload')}
          </button>
          <Link to="/" className="button button-secondary">
            {t('routeError.goToday')}
          </Link>
        </div>
        <ErrorReference reference={reference} />
      </Card>
    </main>
  )
}
