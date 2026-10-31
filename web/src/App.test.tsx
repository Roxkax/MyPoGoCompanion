import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { App } from './App'

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
})
