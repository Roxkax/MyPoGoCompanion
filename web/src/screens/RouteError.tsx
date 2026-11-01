import { Card } from '../ui/Card'

/** Shown if a screen breaks while drawing: a way out instead of a blank page. Error reporting comes with E01-F08. */
export function RouteError() {
  return (
    <main className="screen route-error">
      <Card as="section" aria-labelledby="route-error-title">
        <h1 id="route-error-title">Something went wrong</h1>
        <p>This page couldn’t be shown. Reloading usually fixes it.</p>
        <button type="button" className="button" onClick={() => window.location.reload()}>
          Reload
        </button>
      </Card>
    </main>
  )
}
