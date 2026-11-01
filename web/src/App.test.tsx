import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { App } from './App'
import { readEnvironment } from './environment'

afterEach(() => vi.unstubAllGlobals())

describe('App', () => {
  it('shows the brand, the API status and the fan-project disclaimer', async () => {
    vi.stubGlobal('fetch', () =>
      Promise.resolve(
        Response.json({ status: 'Healthy', version: 'abc1234', checks: { database: 'Healthy' } }),
      ),
    )

    render(
      <QueryClientProvider client={new QueryClient()}>
        <App />
      </QueryClientProvider>,
    )

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

    const { container } = render(
      <QueryClientProvider client={new QueryClient()}>
        <App environment={readEnvironment({ VITE_APP_ENV: kind })} />
      </QueryClientProvider>,
    )

    expect(screen.getByRole('note', { name: `${label} environment` })).toBeInTheDocument()
    expect(screen.getByRole('banner')).toHaveTextContent(`MyPoGoCompanion${label}`)
    expect(container.querySelector('.topbar img')).toHaveAttribute('src', icon)
  })

  it('in production: no banner and no pill', () => {
    vi.stubGlobal('fetch', () => new Promise(() => {}))

    render(
      <QueryClientProvider client={new QueryClient()}>
        <App environment={readEnvironment({ VITE_APP_ENV: 'production' })} />
      </QueryClientProvider>,
    )

    expect(screen.queryByRole('note')).not.toBeInTheDocument()
    expect(screen.getByRole('banner')).toHaveTextContent(/^MyPoGoCompanion$/)
  })
})
