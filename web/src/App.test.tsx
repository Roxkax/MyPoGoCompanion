import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { writePendingSignIn } from './auth/storage'
import { readEnvironment } from './environment'
import { currentAddress, renderApp } from './testing/app'
import { routeFetch, sessionFor } from './testing/auth'

afterEach(() => vi.unstubAllGlobals())

const healthy = () =>
  Response.json({ status: 'Healthy', version: 'abc1234', checks: { database: 'Healthy' } })

describe('App', () => {
  it('opens on Today with the API status', async () => {
    vi.stubGlobal('fetch', routeFetch({ '/health': healthy }))

    renderApp('/')

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Today')
    expect(await screen.findByText('Connected to the API')).toBeInTheDocument()
  })

  it.each([
    ['qa', 'QA', '/icon-qa.svg'],
    ['local', 'Local', '/icon-local.svg'],
  ])('on %s: the banner, a pill next to the name and its own icon', (kind, label, icon) => {
    vi.stubGlobal('fetch', () => new Promise(() => {}))

    renderApp('/', { layout: 'desktop', environment: readEnvironment({ VITE_APP_ENV: kind }) })

    expect(screen.getByRole('note', { name: `${label} environment` })).toBeInTheDocument()
    const brand = screen.getByRole('link', { name: 'MyPoGoCompanion, Today' })
    expect(brand).toHaveTextContent(`MyPoGoCompanion${label}`)
    expect(brand.querySelector('img')).toHaveAttribute('src', icon)
  })

  it('in production: no banner and no pill', () => {
    vi.stubGlobal('fetch', () => new Promise(() => {}))

    renderApp('/', { layout: 'desktop' })

    expect(screen.queryByRole('note')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'MyPoGoCompanion, Today' })).toHaveTextContent(
      /^MyPoGoCompanion$/,
    )
  })

  it('names the browser tab after the screen', async () => {
    vi.stubGlobal('fetch', () => new Promise(() => {}))

    renderApp('/pokedex', { environment: readEnvironment({ VITE_APP_ENV: 'qa' }) })
    expect(document.title).toBe('Pokédex · QA · MyPoGoCompanion')

    within(screen.getByRole('navigation', { name: 'Tabs' }))
      .getByRole('link', { name: 'Today' })
      .click()
    await waitFor(() => expect(document.title).toBe('QA · MyPoGoCompanion'))
  })

  it('on /auth/callback: finishes signing in and goes back to the page', async () => {
    writePendingSignIn({ state: 'abc', verifier: 'v'.repeat(64), returnTo: '/battle/raids?tier=5' })
    const refresh = vi.fn(() => Response.json(sessionFor()))
    vi.stubGlobal(
      'fetch',
      routeFetch({
        '/auth/google': () => Response.json(sessionFor()),
        '/auth/refresh': refresh,
        '/health': healthy,
      }),
    )

    renderApp('/auth/callback?code=c&state=abc')

    expect(await screen.findByRole('heading', { name: 'Raid bosses' })).toBeInTheDocument()
    expect(currentAddress()).toBe('/battle/raids?tier=5')
    expect(screen.getByRole('button', { name: 'Account: Ash' })).toBeInTheDocument()
    expect(refresh).not.toHaveBeenCalled()
  })

  it('on /auth/callback with a bad answer: says so, and "Back to the app" leaves for Today', async () => {
    vi.stubGlobal('fetch', routeFetch({ '/health': healthy }))

    renderApp('/auth/callback?error=access_denied')

    expect(await screen.findByRole('alert')).toBeInTheDocument()
    screen.getByRole('button', { name: 'Back to the app' }).click()
    await waitFor(() => expect(currentAddress()).toBe('/'))
  })
})
