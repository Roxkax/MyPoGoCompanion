import { Link } from 'react-router'
import { DetailScreen } from '../layout/Screen'
import { Card } from '../ui/Card'

/** The content of "Page not found": an address that doesn't exist (or an admin screen for a non-admin). */
export function NotFound() {
  return (
    <Card as="section" className="not-found" aria-labelledby="not-found-title">
      <h2 id="not-found-title">Page not found</h2>
      <p>
        This address doesn’t lead to any page in the app. It may be mistyped, or the page may have
        moved.
      </p>
      <Link to="/" className="button">
        Go to Today
      </Link>
    </Card>
  )
}

export function NotFoundScreen() {
  return (
    <DetailScreen back={{ to: '/', label: 'Today' }}>
      <NotFound />
    </DetailScreen>
  )
}
