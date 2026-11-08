import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { LanguageStore } from '../../i18n/language'
import { currentAddress, renderApp } from '../../testing/app'
import { MemoryStorage } from '../../testing/auth'
import { samplePokedex } from '../../testing/pokedex'
import { garchompPage } from '../../testing/pokemonPage'

let pageAnswer: () => Promise<Response>
let asked: string[]

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

beforeEach(() => {
  asked = []
  pageAnswer = () => Promise.resolve(json(garchompPage))
  vi.stubGlobal('fetch', (input: RequestInfo | URL) => {
    const url = new Request(input).url
    asked.push(new URL(url).pathname)
    if (url.endsWith('/pokedex')) return Promise.resolve(json(samplePokedex))
    if (url.endsWith('/pokedex/445')) return pageAnswer()
    return new Promise(() => {})
  })
})
afterEach(() => vi.unstubAllGlobals())

async function open(
  path = '/pokedex/garchomp',
  layout: 'phone' | 'desktop' = 'phone',
  language?: 'pt-PT',
) {
  const languages = language ? new LanguageStore(new MemoryStorage(), ['pt-PT']) : undefined
  const result = renderApp(path, { layout, languages })
  await screen.findByRole('heading', { level: 1, name: /Garchomp/ })
  return result
}

function section(name: string): HTMLElement {
  return screen.getByRole('region', { name })
}

describe('Pokémon page (phone)', () => {
  it('shows the header and the Overview tab: stats, rankings and type matchups', async () => {
    await open()

    expect(screen.getByText('#445')).toBeInTheDocument()
    expect(asked).toContain('/pokedex/445')
    const versions = within(screen.getByRole('group', { name: 'Versions' }))
    expect(versions.getAllByRole('button').map((b) => b.textContent)).toEqual([
      'Garchomp',
      'Shadow',
      'Mega',
    ])
    expect(versions.getByRole('button', { name: 'Garchomp' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )

    const stats = within(section('Stats'))
    expect(stats.getByText('261')).toBeInTheDocument()
    expect(stats.getByText('4,532')).toBeInTheDocument()

    const rankings = within(section('Rankings'))
    expect(rankings.getByRole('link', { name: /Great League.*Shadow #473.*#477/ })).toHaveAttribute(
      'href',
      '/battle/tiers/great-league',
    )
    expect(rankings.getByRole('link', { name: /Master League.*Not ranked/ })).toBeInTheDocument()
    expect(rankings.getByRole('link', { name: /Raids · Ground.*Mega #2.*#12/ })).toHaveAttribute(
      'href',
      '/battle/tiers/raids/ground',
    )

    const matchups = within(section('Type matchups'))
    expect(matchups.getByText('Ice ×2.56')).toBeInTheDocument()
    expect(matchups.getByText('Electric ×0.24')).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Best IVs' })).not.toBeInTheDocument()
  })

  it('switches tabs through the address, so Back returns to the tab', async () => {
    const user = userEvent.setup()
    await open()

    await user.click(screen.getByRole('button', { name: 'Moves' }))
    expect(window.location.hash).toBe('#moves')
    expect(screen.getByRole('button', { name: 'Moves' })).toHaveAttribute('aria-current', 'page')
    const moves = within(section('Moves'))
    expect(moves.getByText('Raids · Ground')).toBeInTheDocument()
    expect(moves.getByText('Mud Shot + Earth Power')).toBeInTheDocument()
    expect(moves.getByText('Great League & Ultra League')).toBeInTheDocument()
    expect(moves.getByText('Elite TM')).toBeInTheDocument()
    expect(moves.getByText(/\+12 \(3 turns\)/)).toBeInTheDocument()

    window.history.back()
    await waitFor(() =>
      expect(screen.queryByRole('region', { name: 'Moves' })).not.toBeInTheDocument(),
    )
    expect(section('Stats')).toBeInTheDocument()
  })

  it('ranks raid movesets, eight at first, and shows PvP move usage per league', async () => {
    const user = userEvent.setup()
    await open('/pokedex/garchomp#moves')

    const raids = within(section('Raid movesets'))
    expect(raids.getAllByRole('row')).toHaveLength(9)
    await user.click(raids.getByRole('button', { name: 'Show all 10' }))
    expect(raids.getAllByRole('row')).toHaveLength(11)
    expect(raids.getByText('100%')).toBeInTheDocument()
    await user.click(raids.getByRole('button', { name: 'Show fewer' }))
    expect(raids.getAllByRole('row')).toHaveLength(9)

    const usage = within(section('PvP moves'))
    expect(
      usage.getByText(/Recommended: Dragon Tail \+ Earthquake, Fire Blast/),
    ).toBeInTheDocument()
    await user.click(usage.getByRole('radio', { name: 'Ultra League' }))
    expect(usage.getByRole('radio', { name: 'Ultra League' })).toBeChecked()
  })

  it('shows the best IVs with the bars to keep, and checks any IVs', async () => {
    const user = userEvent.setup()
    await open('/pokedex/garchomp#ivs')

    const best = within(section('Best IVs'))
    expect(best.getByText('0/15/14')).toBeInTheDocument()
    expect(best.getAllByText('Keep: Atk 0–5 · Def 11–15 · HP 6–15')).toHaveLength(2)
    expect(best.getByText('15/15/15')).toBeInTheDocument()

    const check = within(section('Check any IVs'))
    const attack = check.getByRole('spinbutton', { name: 'Attack' })
    const hp = check.getByRole('spinbutton', { name: 'HP' })
    await user.clear(attack)
    await user.type(attack, '0')
    await user.clear(hp)
    await user.type(hp, '14')
    expect(check.getByText('#1 · 100%')).toBeInTheDocument()
    expect(check.getByText('Level 14 · CP 1,495')).toBeInTheDocument()

    await user.clear(hp)
    await user.type(hp, '99')
    await user.tab()
    expect(hp).toHaveValue(15)

    const cheat = within(section('100% CP cheat sheet'))
    expect(cheat.getByText('2,264')).toBeInTheDocument()
  })

  it('shows where to get it right now, what is coming, and the history, then the evolution', async () => {
    await open('/pokedex/garchomp#getIt')

    const getIt = within(section('How to get it'))
    expect(getIt.getByText('Mega raids')).toBeInTheDocument()
    expect(getIt.getByText(/Mega Garchomp · until/)).toBeInTheDocument()
    expect(getIt.getByText('Garchomp Spotlight')).toBeInTheDocument()
    expect(getIt.getByText(/Win 3 raids → Garchomp · Dragon Week/)).toBeInTheDocument()

    const evolution = within(section('Evolution'))
    expect(evolution.getByRole('link', { name: 'Gible' })).toHaveAttribute('href', '/pokedex/gible')
    expect(evolution.getByRole('link', { name: 'Garchomp' })).toHaveAttribute(
      'aria-current',
      'page',
    )
    expect(evolution.getByText('100 candy')).toBeInTheDocument()
    expect(evolution.getByText('5 km')).toBeInTheDocument()
  })

  it('shows every version and the costumes', async () => {
    await open('/pokedex/garchomp#versions')

    const versions = within(section('Versions'))
    for (const name of ['Shiny', 'Shadow', 'Purified', 'Mega Garchomp', 'Holiday 2025']) {
      expect(versions.getByText(name)).toBeInTheDocument()
    }
  })

  it('switches versions through the address', async () => {
    const user = userEvent.setup()
    await open()

    await user.click(screen.getByRole('button', { name: 'Mega' }))
    expect(currentAddress()).toBe('/pokedex/garchomp?version=garchomp_mega')
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Mega Garchomp')

    await user.click(screen.getByRole('button', { name: 'Shadow' }))
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Shadow Garchomp')
    expect(within(section('Stats')).getByText(/Same stats as Shadow/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Garchomp' }))
    expect(currentAddress()).toBe('/pokedex/garchomp')
  })

  it('says so for a name that isn’t in the Pokédex', async () => {
    renderApp('/pokedex/missingno')
    expect(
      await screen.findByText('There is no Pokémon called “missingno” in the Pokédex.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Go to the Pokédex' })).toHaveAttribute(
      'href',
      '/pokedex',
    )
  })

  it('offers to try again when the page can’t be loaded', async () => {
    const user = userEvent.setup()
    pageAnswer = () => Promise.resolve(json({ title: 'Boom', status: 500 }, 500))
    renderApp('/pokedex/garchomp')
    const retry = await screen.findByRole('button', { name: /try again/i })
    pageAnswer = () => Promise.resolve(json(garchompPage))
    await user.click(retry)
    expect(await screen.findByRole('heading', { level: 1, name: 'Garchomp' })).toBeInTheDocument()
  })

  it('uses the game’s Portuguese names', async () => {
    await open('/pokedex/garchomp?version=garchomp_mega', 'phone', 'pt-PT')
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Mega Garchomp')
    expect(within(section('Atributos')).getByText('PC máx. · N50')).toBeInTheDocument()
  })
})

describe('Pokémon page (desktop)', () => {
  it('shows everything at once in two columns', async () => {
    await open('/pokedex/garchomp', 'desktop')
    for (const name of [
      'Stats',
      'Rankings',
      'Moves',
      'Raid movesets',
      'PvP moves',
      'Versions',
      'Best IVs',
      'Check any IVs',
      '100% CP cheat sheet',
      'Type matchups',
      'Evolution',
      'How to get it',
    ]) {
      expect(section(name)).toBeInTheDocument()
    }
    expect(
      screen.queryByRole('navigation', { name: 'Pokémon page sections' }),
    ).not.toBeInTheDocument()
  })
})
