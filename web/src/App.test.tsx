import { act, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { App } from './App'
import { writePendingSignIn } from './auth/storage'
import { readEnvironment } from './environment'
import { ash, renderWithAuth, routeFetch, sessionFor, testSession } from './testing/auth'

afterEach(() => {
  vi.unstubAllGlobals()
  window.history.replaceState(null, '', '/')
})

const healthy = () =>
  Response.json({ status: 'Healthy', version: 'abc1234', checks: { database: 'Healthy' } })

describe('App', () => {
  it('shows the brand, the API status and the fan-project disclaimer', async () => {
    vi.stubGlobal('fetch', routeFetch({ '/health': healthy }))

    renderWithAuth(<App />)

    expect(screen.getByText('MyPoGoCompanion')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Coming soon')
    expect(await screen.findByText('Connected to the API')).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toHaveTextContent('Unofficial fan project')
  })

  it.each([
    ['qa', 'QA', '/icon-qa.svg'],
    ['local', 'Local', '/icon-local.svg'],
  ])('on %s: the banner, a pill next to the name and its own icon', (kind, label, icon) => {
    vi.stubGlobal('fetch', () => new Promise(() => {}))

    const { container } = renderWithAuth(
      <App environment={readEnvironment({ VITE_APP_ENV: kind })} />,
    )

    expect(screen.getByRole('note', { name: `${label} environment` })).toBeInTheDocument()
    expect(screen.getByRole('banner')).toHaveTextContent(`MyPoGoCompanion${label}`)
    expect(container.querySelector('.topbar img')).toHaveAttribute('src', icon)
  })

  it('in production: no banner and no pill', () => {
    vi.stubGlobal('fetch', () => new Promise(() => {}))

    renderWithAuth(<App environment={readEnvironment({ VITE_APP_ENV: 'production' })} />)

    expect(screen.queryByRole('note')).not.toBeInTheDocument()
    expect(screen.getByRole('banner')).toHaveTextContent(/^MyPoGoCompanion$/)
  })

  it('signed out: the profile button signs in and the collection prompt shows', async () => {
    vi.stubGlobal('fetch', () => new Promise(() => {}))

    renderWithAuth(<App />)

    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Your collection lives here' })).toBeInTheDocument()
  })

  it('signed in: the account button and who is signed in', () => {
    vi.stubGlobal('fetch', () => new Promise(() => {}))
    const { session } = testSession()
    renderWithAuth(<App />, { session })

    act(() => session.accept(sessionFor()))

    expect(screen.getByRole('button', { name: 'Account: Ash' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Signed in' })).toHaveTextContent(ash.email)
    expect(
      screen.queryByRole('region', { name: 'Your collection lives here' }),
    ).not.toBeInTheDocument()
  })

  it('on /auth/callback: finishes signing in and goes back to the page', async () => {
    writePendingSignIn({ state: 'abc', verifier: 'v'.repeat(64), returnTo: '/raids?tier=5' })
    window.history.replaceState(null, '', '/auth/callback?code=c&state=abc')
    vi.stubGlobal(
      'fetch',
      routeFetch({ '/auth/google': () => Response.json(sessionFor()), '/health': healthy }),
    )

    renderWithAuth(<App />, { pathname: '/auth/callback' })

    expect(await screen.findByRole('heading', { level: 1 })).toHaveTextContent('Coming soon')
    expect(window.location.pathname + window.location.search).toBe('/raids?tier=5')
    expect(screen.getByRole('region', { name: 'Signed in' })).toBeInTheDocument()
  })
})
