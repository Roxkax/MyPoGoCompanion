import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { renderApp } from '../../testing/app'

afterEach(() => vi.unstubAllGlobals())

it('retries a check and hides its copy control when the user changes the expression', async () => {
  const check = vi
    .fn()
    .mockResolvedValueOnce(new Response(null, { status: 503 }))
    .mockResolvedValueOnce(Response.json({ status: 'Valid', parts: ['fire'], term: null }))
    .mockImplementation(() => new Promise(() => {}))
  vi.stubGlobal('fetch', (request: Request) =>
    request.url.endsWith('/filter-search/check') ? check(request) : new Promise(() => {}),
  )

  renderApp('/filters/builder')
  const expression = screen.getByRole('textbox', { name: /^Search string/ })
  fireEvent.change(expression, { target: { value: 'fire' } })
  expect(await screen.findByText('Could not check the search.')).toBeVisible()
  await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
  expect(await screen.findByRole('button', { name: /^Copy$/ })).toBeVisible()

  fireEvent.change(expression, { target: { value: 'fire&' } })
  expect(screen.queryByRole('button', { name: /^Copy$/ })).not.toBeInTheDocument()
  expect(
    screen.getByText('Complete the search expression to edit its conditions here.'),
  ).toBeVisible()
  await waitFor(() => expect(check).toHaveBeenCalledTimes(3))
  const request = check.mock.calls.at(-1)?.[0] as Request
  expect(await request.json()).toEqual({ expression: 'fire&' })
})
