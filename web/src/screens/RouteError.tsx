import { useTranslation } from 'react-i18next'
import { Card } from '../ui/Card'

/** Shown if a screen breaks while drawing: a way out instead of a blank page. Error reporting comes with E01-F08. */
export function RouteError() {
  const { t } = useTranslation()
  return (
    <main className="screen route-error">
      <Card as="section" aria-labelledby="route-error-title">
        <h1 id="route-error-title">{t('routeError.title')}</h1>
        <p>{t('routeError.text')}</p>
        <button type="button" className="button" onClick={() => window.location.reload()}>
          {t('routeError.reload')}
        </button>
      </Card>
    </main>
  )
}
