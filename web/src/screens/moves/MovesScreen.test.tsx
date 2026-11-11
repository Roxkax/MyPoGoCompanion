import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Schemas } from '../../api/client'
import { LanguageStore } from '../../i18n/language'
import { currentAddress, renderApp } from '../../testing/app'
import { MemoryStorage } from '../../testing/auth'

type Move = Schemas['PageMove']

const base: Move = {
  id: 'DRAGON_CLAW',
  nameEn: 'Dragon Claw',
  namePt: 'Garra de Dragão',
  type: 'dragon',
  kind: 'Charged',
  raidPower: 50,
  raidDurationMs: 1500,
  raidEnergy: -33,
  pvpPower: 80,
  pvpEnergy: -50,
  pvpTurns: null,
  pvpBuffChance: null,
  pvpSelfAttack: 0,
  pvpSelfDefense: 0,
  pvpOpponentAttack: 0,
  pvpOpponentDefense: 0,
}

const moves: Move[] = [
  base,
  {
    ...base,
    id: 'EARTH_POWER',
    nameEn: 'Earth Power',
    namePt: 'Poder da Terra',
    type: 'ground',
    raidPower: 100,
    raidDurationMs: 3500,
    raidEnergy: -50,
    pvpPower: 90,
    pvpEnergy: -55,
    pvpBuffChance: 0.1,
    pvpOpponentDefense: -1,
  },
  {
    ...base,
    id: 'SUPER_EFFECT',
    nameEn: 'Super Effect',
    namePt: null,
    type: 'stellar',
    pvpBuffChance: null,
    pvpSelfAttack: 1,
    pvpSelfDefense: -1,
    pvpOpponentAttack: -2,
  },
  {
    ...base,
    id: 'PVP_ONLY',
    nameEn: 'PvP Only',
    type: 'normal',
    raidPower: 0,
    raidDurationMs: 0,
    raidEnergy: 0,
    pvpBuffChance: 0,
    pvpOpponentAttack: -1,
  },
  {
    ...base,
    id: 'DRAGON_TAIL',
    nameEn: 'Dragon Tail',
    namePt: 'Cauda do Dragão',
    kind: 'Fast',
    raidPower: 14,
    raidDurationMs: 1000,
    raidEnergy: 8,
    pvpPower: 9,
    pvpEnergy: 12,
    pvpTurns: 3,
  },
]

let answer: () => Promise<Response>

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status === 200 ? 'application/json' : 'application/problem+json' },
  })
}

beforeEach(() => {
  answer = () => Promise.resolve(json({ moves }))
  vi.stubGlobal('fetch', (input: RequestInfo | URL) => {
    const url = new Request(input).url
    return url.endsWith('/moves') ? answer() : new Promise(() => {})
  })
})
afterEach(() => vi.unstubAllGlobals())

async function open(path = '/pokedex/moves', layout: 'phone' | 'desktop' = 'phone') {
  renderApp(path, { layout })
  await screen.findByRole('table')
}

function row(id: string) {
  const found = document.querySelector<HTMLElement>(`[data-move="${id}"]`)
  if (!found) throw new Error(`${id} is not on the list`)
  return within(found)
}

describe('Moves list', () => {
  it('shows signed raid numbers, searches, clears, and filters by type on a phone', async () => {
    const user = userEvent.setup()
    await open()

    expect(screen.getByRole('table', { name: 'Charged moves' })).toBeInTheDocument()
    expect(screen.getByText('4 charged moves')).toBeInTheDocument()
    expect(row('DRAGON_CLAW').getByText('−33')).toBeInTheDocument()
    expect(row('PVP_ONLY').getAllByText('—')).toHaveLength(3)
    expect(row('PVP_ONLY').getAllByTitle('Not available in raids')).toHaveLength(3)
    expect(row('DRAGON_CLAW').getByRole('link')).toHaveAttribute(
      'href',
      '/pokedex/moves/dragon-claw',
    )

    const searchbox = screen.getByRole('searchbox', { name: 'Search moves' })
    await user.type(searchbox, 'claw')
    expect(screen.getByText('1 charged move')).toBeInTheDocument()
    expect(currentAddress()).toBe('/pokedex/moves?q=claw')
    await user.click(screen.getByRole('button', { name: 'Clear search' }))

    await user.click(screen.getByRole('button', { name: 'Type ▾' }))
    const types = screen.getByRole('dialog', { name: 'Type' })
    await user.click(within(types).getByRole('button', { name: 'Dragon' }))
    expect(currentAddress()).toBe('/pokedex/moves?type=dragon')
    expect(screen.getAllByText('Dragon')).not.toHaveLength(0)
    await user.click(screen.getByRole('button', { name: 'Remove Dragon' }))
    expect(currentAddress()).toBe('/pokedex/moves')
  })

  it('switches to PvP, shows effects, sorts, then switches to fast moves', async () => {
    const user = userEvent.setup()
    await open()

    await user.click(screen.getByRole('radio', { name: 'PvP' }))
    expect(currentAddress()).toContain('mode=pvp')
    expect(row('EARTH_POWER').getByText('Foe Def −1')).toBeInTheDocument()
    expect(row('EARTH_POWER').getByText('10% chance')).toBeInTheDocument()
    expect(row('SUPER_EFFECT').getByText('Self Atk +1')).toBeInTheDocument()
    expect(row('SUPER_EFFECT').getByText('Self Def −1')).toBeInTheDocument()
    expect(row('SUPER_EFFECT').getByText('Foe Atk −2')).toBeInTheDocument()
    expect(row('SUPER_EFFECT').getByText('Chance unknown')).toBeInTheDocument()
    expect(row('PVP_ONLY').getByText('—')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /^Sort:/ }))
    const sort = screen.getByRole('dialog', { name: 'Sort by' })
    await user.click(within(sort).getByRole('button', { name: /^Name/ }))
    expect(currentAddress()).toContain('sort=name')
    await user.keyboard('{Escape}')
    await user.click(screen.getByRole('radio', { name: /^Fast/ }))
    expect(screen.getByRole('table', { name: 'Fast moves' })).toBeInTheDocument()
    expect(row('DRAGON_TAIL').getByText('+12')).toBeInTheDocument()
    expect(row('DRAGON_TAIL').getByText('3')).toBeInTheDocument()
  })

  it('shows both number groups on desktop and sorts from a column heading', async () => {
    const user = userEvent.setup()
    await open('/pokedex/moves?type=ground', 'desktop')

    expect(screen.getByRole('heading', { name: 'Moves' })).toBeInTheDocument()
    expect(screen.getByText('5 moves')).toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: 'Raids' })).toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: 'PvP (trainer battles)' })).toBeInTheDocument()
    expect(row('EARTH_POWER').getByText('Ground')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'PvP power' }))
    expect(currentAddress()).toContain('sort=pvpPower')
    await user.click(screen.getByRole('button', { name: 'Type: Ground ▾' }))
    await user.click(
      within(screen.getByRole('dialog', { name: 'Type' })).getByRole('button', {
        name: 'All types',
      }),
    )
    expect(row('SUPER_EFFECT').getByText('stellar')).toBeInTheDocument()
  })

  it('uses Portuguese move names and English fallback', async () => {
    const languages = new LanguageStore(new MemoryStorage(), ['pt-PT'])
    renderApp('/pokedex/moves?mode=pvp', { layout: 'phone', languages })
    await screen.findByRole('table')

    expect(row('DRAGON_CLAW').getByText('Garra de Dragão')).toBeInTheDocument()
    expect(row('SUPER_EFFECT').getByText('Super Effect')).toBeInTheDocument()
    expect(row('EARTH_POWER').getByText('Adversário Def −1')).toBeInTheDocument()
  })

  it('explains no matches and clears while preserving kind and mode', async () => {
    const user = userEvent.setup()
    renderApp('/pokedex/moves?q=nope&kind=fast&mode=pvp')
    await screen.findByRole('heading', { name: 'No moves match' })

    await user.click(screen.getByRole('button', { name: 'Clear search and filters' }))
    expect(currentAddress()).toBe('/pokedex/moves?kind=fast&mode=pvp')
    expect(await screen.findByRole('table', { name: 'Fast moves' })).toBeInTheDocument()
  })

  it('shows an error and retries the request', async () => {
    const user = userEvent.setup()
    answer = () => Promise.resolve(json({ detail: 'Move service unavailable', status: 503 }, 503))
    renderApp('/pokedex/moves')

    expect(
      await screen.findByRole('heading', { name: 'This couldn’t be loaded' }),
    ).toBeInTheDocument()
    answer = () => Promise.resolve(json({ moves }))
    await user.click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByRole('table')).toBeInTheDocument()
  })

  it('shows loading while the move answer is pending', () => {
    answer = () => new Promise(() => {})
    renderApp('/pokedex/moves')

    expect(screen.getByRole('status')).toHaveTextContent('Loading moves…')
  })
})
