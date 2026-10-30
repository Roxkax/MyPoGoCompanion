import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiStatus } from './ApiStatus'

afterEach(() => vi.unstubAllGlobals())

function renderStatus() {
  const client = new QueryClient({ defaultOptions: { queries: { retryDelay: 0 } } })
  return render(
    <QueryClientProvider client={client}>
      <ApiStatus />
    </QueryClientProvider>,
  )
}

describe('ApiStatus', () => {
  it('shows the version when the API and database work', async () => {
    vi.stubGlobal('fetch', () =>
      Promise.resolve(
        Response.json({ status: 'Healthy', version: 'abc1234', checks: { database: 'Healthy' } }),
      ),
    )

    renderStatus()

    expect(screen.getByRole('status')).toHaveTextContent('Checking the API…')
    expect(await screen.findByText('Connected to the API')).toBeInTheDocument()
    expect(screen.getByText('Version abc1234 · database working')).toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('warns when the database is down', async () => {
    vi.stubGlobal('fetch', () =>
      Promise.resolve(
        Response.json(
          { status: 'Unhealthy', version: 'abc1234', checks: { database: 'Unhealthy' } },
          { status: 503 },
        ),
      ),
    )

    renderStatus()

    expect(
      await screen.findByText('The API is running, but its database isn’t answering'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument()
  })

  it('offers to try again when the API is unreachable, and recovers', async () => {
    const fetchMock = vi
      .fn()
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
      .mockResolvedValue(
        Response.json({ status: 'Healthy', version: 'abc1234', checks: { database: 'Healthy' } }),
      )
    vi.stubGlobal('fetch', fetchMock)

    renderStatus()
    await userEvent.click(await screen.findByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('Connected to the API')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledTimes(3)
  })
})
