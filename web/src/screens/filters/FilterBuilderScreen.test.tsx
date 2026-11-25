import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { LanguageStore } from '../../i18n/language'
import { renderApp } from '../../testing/app'
import { MemoryStorage } from '../../testing/auth'
import { samplePokedex } from '../../testing/pokedex'

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

function mockBuilder({
  catalog = () => Promise.resolve(Response.json(samplePokedex)),
  result = (expression: string) => ({ status: 'Valid', parts: [expression], term: null }),
}: {
  catalog?: () => Promise<Response>
  result?: (expression: string) => { status: string; parts: string[]; term: string | null }
} = {}) {
  vi.stubGlobal('fetch', async (request: Request) => {
    const path = new URL(request.url).pathname
    if (path === '/pokedex') return catalog()
    if (path === '/filter-search/check') {
      const body = (await request.json()) as { expression: string }
      return Response.json(result(body.expression))
    }
    return new Promise(() => {})
  })
}

const editor = () => screen.getByRole('textbox', { name: /^Search string/ })

it('finds Pokémon in the desktop picker and keeps a selected family while filtering choices', async () => {
  mockBuilder()
  renderApp('/filters/builder', { layout: 'desktop' })
  const picker = screen.getByRole('region', { name: 'Add a condition' })
  const query = within(picker).getByRole('searchbox')
  fireEvent.change(query, { target: { value: 'xyz missing' } })
  expect(within(picker).getByText('No matches.')).toBeVisible()
  fireEvent.change(query, { target: { value: 'names' } })
  fireEvent.click(within(picker).getByRole('button', { name: /^Pokémon/ }))
  expect(await screen.findByText('Search to see more matches.')).toBeVisible()
  const search = screen.getByRole('searchbox', { name: 'Search values' })
  fireEvent.change(search, { target: { value: 'pikachu' } })
  fireEvent.click(screen.getByRole('checkbox', { name: /Pikachu · #25/ }))
  fireEvent.click(screen.getByRole('checkbox', { name: 'Include the whole evolution family' }))
  const family = screen.getByRole('checkbox', { name: /Pikachu · #25/ })
  fireEvent.click(family)
  fireEvent.click(family)
  expect(family).not.toBeChecked()
  fireEvent.click(family)
  fireEvent.change(search, { target: { value: 'does not exist' } })
  expect(screen.getByText('No matches.')).toBeVisible()
  expect(screen.getByRole('button', { name: 'Remove Pikachu family' })).toBeVisible()
  fireEvent.click(screen.getByRole('button', { name: 'Apply condition' }))
  expect(editor()).toHaveValue('25,+pikachu')

  fireEvent.click(screen.getByRole('button', { name: /^Remove Pokémon/ }))
  expect(editor()).toHaveValue('')
})

it('adds an excluded tag in the phone sheet and can cancel editing it', async () => {
  mockBuilder()
  renderApp('/filters/builder')
  fireEvent.click(screen.getByRole('button', { name: 'Add a condition' }))
  fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: /^Tags/ }))
  fireEvent.click(screen.getByRole('checkbox', { name: 'Exclude values I add' }))
  const tag = screen.getByRole('textbox', { name: 'Tag name' })
  fireEvent.change(tag, { target: { value: '#Raid team' } })
  fireEvent.click(screen.getByRole('button', { name: 'Add value' }))
  fireEvent.change(tag, { target: { value: '#Raid team' } })
  fireEvent.click(screen.getByRole('button', { name: 'Add value' }))
  expect(screen.getAllByRole('button', { name: 'Remove not #Raid team' })).toHaveLength(1)
  fireEvent.click(screen.getByRole('button', { name: 'Apply condition' }))
  expect(editor()).toHaveValue('!#Raid team')
  fireEvent.click(screen.getByRole('button', { name: /^Edit Tags/ }))
  fireEvent.click(screen.getByRole('button', { name: 'Remove not #Raid team' }))
  expect(screen.getByRole('button', { name: 'Apply condition' })).toBeDisabled()
  fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))
  expect(editor()).toHaveValue('!#Raid team')
  fireEvent.click(screen.getByRole('button', { name: 'Clear' }))
  expect(editor()).toHaveValue('')
})

it('adds specific moves and slotted move types after retrying the catalog', async () => {
  let resolveCatalog: (response: Response) => void = () => {}
  const catalog = vi
    .fn()
    .mockImplementationOnce(
      () =>
        new Promise<Response>((resolve) => {
          resolveCatalog = resolve
        }),
    )
    .mockResolvedValueOnce(Response.json(samplePokedex))
  mockBuilder({ catalog })
  renderApp('/filters/builder', { layout: 'desktop' })
  fireEvent.click(screen.getByRole('button', { name: /Move names, types/ }))
  expect(screen.getByText('Loading game data…')).toBeVisible()
  resolveCatalog(new Response(null, { status: 503 }))
  expect(await screen.findByText('Could not load game data.')).toBeVisible()
  fireEvent.click(screen.getByRole('button', { name: 'Try again' }))
  const search = screen.getByRole('searchbox', { name: 'Search values' })
  fireEvent.change(search, { target: { value: 'earthquake' } })
  fireEvent.click(await screen.findByRole('checkbox', { name: 'Earthquake' }))
  fireEvent.change(screen.getByRole('combobox', { name: 'Move slot for types' }), {
    target: { value: '3' },
  })
  fireEvent.change(search, { target: { value: 'ghost' } })
  fireEvent.click(screen.getByRole('checkbox', { name: 'Ghost move' }))
  fireEvent.click(screen.getByRole('button', { name: 'Apply condition' }))
  expect(editor()).toHaveValue('@earthquake,@3ghost')
})

it('edits an appraisal range and preserves unrecognized alternatives in a custom condition', async () => {
  mockBuilder()
  renderApp('/filters/builder', { layout: 'desktop' })
  fireEvent.change(editor(), { target: { value: 'fire,custom nickname&cp500-&4defense' } })
  expect(screen.getByRole('button', { name: /Edit CP & HP: CP at least 500/ })).toBeVisible()
  fireEvent.click(screen.getByRole('button', { name: /Edit Appraisal: Defense bars 4/ }))
  fireEvent.change(screen.getByRole('combobox', { name: 'Value to filter' }), {
    target: { value: 'hpBar' },
  })
  fireEvent.change(screen.getByRole('spinbutton', { name: 'Minimum' }), { target: { value: '2' } })
  fireEvent.change(screen.getByRole('spinbutton', { name: 'Maximum' }), { target: { value: '4' } })
  fireEvent.click(screen.getByRole('button', { name: 'Add value' }))
  expect(screen.getByRole('button', { name: 'Remove HP bars 2–4' })).toBeVisible()
  fireEvent.click(screen.getByRole('button', { name: 'Apply condition' }))
  fireEvent.click(screen.getByRole('button', { name: /Edit Custom condition/ }))
  fireEvent.change(screen.getByRole('textbox', { name: 'Search term' }), {
    target: { value: 'other nickname' },
  })
  fireEvent.click(screen.getByRole('button', { name: 'Add value' }))
  fireEvent.click(screen.getByRole('button', { name: 'Apply condition' }))
  expect(editor()).toHaveValue('fire,custom nickname,other nickname&cp500-&4defense,2-4hp')
})

it('shows separate copy controls for an unverified multipart search and blocks oversized text', async () => {
  mockBuilder({
    result: () => ({
      status: 'Unverified',
      parts: ['my nickname&fire', '25&fire'],
      term: 'my nickname',
    }),
  })
  renderApp('/filters/builder')
  fireEvent.change(editor(), { target: { value: 'my nickname,25&fire' } })
  expect(await screen.findByText(/Could not verify “my nickname”/)).toBeVisible()
  expect(screen.getByRole('button', { name: 'Copy · Part 1' })).toBeVisible()
  expect(screen.getByRole('button', { name: 'Copy · Part 2' })).toBeVisible()
  fireEvent.change(editor(), { target: { value: 'x'.repeat(19_501) } })
  expect(screen.getByText(/This search is too long to check/)).toBeVisible()
  expect(screen.queryByRole('button', { name: /^Copy · Part/ })).not.toBeInTheDocument()

  fireEvent.change(editor(), { target: { value: '   ' } })
  expect(screen.getByText('Enter a search to check it.')).toBeVisible()
})

it('shows Portuguese values while building a search with English keywords', async () => {
  const catalog = () =>
    Promise.resolve(
      Response.json({
        species: [
          { dex: 25, nameEn: 'Pikachu', namePt: null },
          { dex: 26, nameEn: 'Raichu', namePt: 'Raichu' },
        ],
        moves: [
          { nameEn: 'Earthquake', namePt: 'Terremoto' },
          { nameEn: 'Scratch', namePt: null },
        ],
      }),
    )
  mockBuilder({ catalog })
  renderApp('/filters/builder', {
    layout: 'desktop',
    languages: new LanguageStore(new MemoryStorage(), ['pt-PT']),
  })
  fireEvent.click(screen.getByRole('button', { name: /Nomes, tipos e ataques/ }))
  fireEvent.click(await screen.findByRole('checkbox', { name: 'Terremoto' }))
  fireEvent.click(screen.getByRole('button', { name: 'Aplicar condição' }))
  expect(screen.getByRole('textbox', { name: /^Texto de pesquisa/ })).toHaveValue('@earthquake')
})
