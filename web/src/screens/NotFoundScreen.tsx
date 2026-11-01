import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { DetailScreen } from '../layout/Screen'
import { Card } from '../ui/Card'

/** The content of "Page not found": an address that doesn't exist (or an admin screen for a non-admin). */
export function NotFound() {
  const { t } = useTranslation()
  return (
    <Card as="section" className="not-found" aria-labelledby="not-found-title">
      <h2 id="not-found-title">{t('titles.pageNotFound')}</h2>
      <p>{t('notFound.text')}</p>
      <Link to="/" className="button">
        {t('notFound.goToday')}
      </Link>
    </Card>
  )
}

export function NotFoundScreen() {
  return (
    <DetailScreen back={{ to: '/', label: 'today' }}>
      <NotFound />
    </DetailScreen>
  )
}
