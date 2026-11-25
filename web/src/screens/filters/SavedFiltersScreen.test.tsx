import { act, fireEvent, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { renderApp } from '../../testing/app'
import { ash, sessionFor } from '../../testing/auth'
import { rememberFilterDraft } from './savedFilters'

const filter = {
  id: '0190e0e0-0000-7000-8000-000000000123',
  name: 'Raid prep',
  searchString: 'fire;water&!shiny',
  createdAt: '2026-11-24T15:00:00Z',
  updatedAt: '2026-11-24T15:00:00Z',
}

afterEach(() => {
  vi.unstubAllGlobals()
  sessionStorage.clear()
})

it('offers sign-in from a valid draft and restores the draft after the return', async () => {
  vi.stubGlobal('fetch', async (request: Request) =>
    request.url.endsWith('/filter-search/check')
      ? Response.json({ status: 'Valid', parts: ['fire'] })
      : new Promise(() => {}),
  )
  const first = renderApp('/filters/builder')
  fireEvent.change(screen.getByRole('textbox', { name: /^Search string/ }), {
    target: { value: 'fire' },
  })
  await userEvent.click(await screen.findByRole('button', { name: 'Save filter' }))
  expect(screen.getByRole('dialog', { name: 'Sign in to make it yours' })).toBeVisible()
  first.unmount()

  renderApp('/filters/builder?resume=1', { user: ash })
  expect(screen.getByRole('textbox', { name: /^Search string/ })).toHaveValue('fire')
  expect(sessionStorage).toHaveLength(0)
})

it('keeps the name after a failed save and submits the unchanged search when retried', async () => {
  const save = vi
    .fn()
    .mockResolvedValueOnce(new Response(null, { status: 500 }))
    .mockResolvedValueOnce(Response.json(filter, { status: 201 }))
  vi.stubGlobal('fetch', async (request: Request) => {
    if (request.url.endsWith('/filter-search/check'))
      return Response.json({ status: 'Valid', parts: [filter.searchString] })
    if (request.url.endsWith('/saved-filters')) return save(await request.json())
    return new Promise(() => {})
  })
  renderApp('/filters/builder', { user: ash })
  fireEvent.change(screen.getByRole('textbox', { name: /^Search string/ }), {
    target: { value: filter.searchString },
  })
  await userEvent.click(await screen.findByRole('button', { name: 'Save filter' }))
  const dialog = screen.getByRole('dialog', { name: 'Save this filter' })
  const name = within(dialog).getByRole('textbox', { name: 'Name' })
  const submit = within(dialog).getByRole('button', { name: 'Save' })
  expect(submit).toBeDisabled()
  await userEvent.type(name, '  Raid prep  ')
  await userEvent.click(submit)
  expect(await within(dialog).findByRole('alert')).toHaveTextContent('Could not finish')
  expect(name).toHaveValue('  Raid prep  ')
  await userEvent.click(submit)
  expect(await screen.findByRole('link', { name: 'View saved filters' })).toBeVisible()
  expect(save).toHaveBeenLastCalledWith({ name: filter.name, searchString: filter.searchString })
})

it('loads saved filters with the current session and removes the old account’s rows on account change', async () => {
  const misty = { ...ash, id: '0190e0e0-0000-7000-8000-000000000124', name: 'Misty' }
  vi.stubGlobal('fetch', async (request: Request) => {
    if (!request.url.endsWith('/saved-filters')) return new Promise(() => {})
    return Response.json(
      request.headers.get('Authorization') === 'Bearer access-for-refresh-2' ? [filter] : [],
    )
  })
  const { session } = renderApp('/filters/saved', { user: ash })
  expect(await screen.findByRole('link', { name: filter.name })).toBeVisible()
  act(() => session.accept(sessionFor(misty, 'misty-refresh')))
  expect(screen.queryByRole('link', { name: filter.name })).not.toBeInTheDocument()
  expect(await screen.findByText('You haven’t saved any filters yet.')).toBeVisible()
})

it('reopens a saved expression and reconstructs its editable condition groups', async () => {
  vi.stubGlobal('fetch', async (request: Request) =>
    request.url.endsWith(`/saved-filters/${filter.id}`)
      ? Response.json(filter)
      : new Promise(() => {}),
  )
  renderApp(`/filters/builder?saved=${filter.id}`, { user: ash })
  expect(await screen.findByRole('textbox', { name: /^Search string/ })).toHaveValue(
    filter.searchString,
  )
  expect(screen.getByRole('button', { name: 'Edit Types: Fire or Water' })).toBeVisible()
})

it('opens a parts sheet when a saved search exceeds the game limit', async () => {
  const search = 'fire,'.repeat(410) + 'water'
  vi.stubGlobal('fetch', async (request: Request) => {
    if (request.url.endsWith('/saved-filters'))
      return Response.json([{ ...filter, searchString: search }])
    if (request.url.endsWith('/filter-search/check'))
      return Response.json({ status: 'Valid', parts: ['fire', 'water'] })
    return new Promise(() => {})
  })
  renderApp('/filters/saved', { user: ash })
  await userEvent.click(await screen.findByRole('button', { name: /^Copy$/ }))
  const dialog = screen.getByRole('dialog', { name: 'Copy' })
  expect(await within(dialog).findAllByRole('button', { name: /^Copy · Part/ })).toHaveLength(2)
})

it('starts a fresh Builder from the Saved screen', async () => {
  rememberFilterDraft('fire')
  vi.stubGlobal('fetch', () => new Promise(() => {}))
  renderApp('/filters/saved', { user: ash })
  await userEvent.click(screen.getByRole('link', { name: 'New filter' }))
  expect(screen.getByRole('textbox', { name: /^Search string/ })).toHaveValue('')
})
