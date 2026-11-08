import { act, fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { LanguageStore } from '../../i18n/language'
import { currentAddress, renderApp } from '../../testing/app'
import { MemoryStorage } from '../../testing/auth'
import { samplePokedex } from '../../testing/pokedex'

let answer: () => Promise<Response>

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

beforeEach(() => {
  answer = () => Promise.resolve(json(samplePokedex))
  vi.stubGlobal('fetch', (input: RequestInfo | URL) => {
    const url = new Request(input).url
    return url.endsWith('/pokedex') ? answer() : new Promise(() => {})
  })
})
afterEach(() => vi.unstubAllGlobals())

/** The line of a Pokédex number (phone card or desktop table row). */
function line(dex: number): HTMLElement {
  const found = document.querySelector<HTMLElement>(`[data-dex="${dex}"]`)
  if (!found) throw new Error(`#${dex} isn't on the list`)
  return found
}

function shownDexes(): number[] {
  return [...document.querySelectorAll<HTMLElement>('[data-dex]')].map((el) =>
    Number(el.dataset.dex),
  )
}

async function openPokedex(
  path = '/pokedex',
  layout: 'phone' | 'desktop' = 'phone',
  language?: 'pt-PT',
) {
  const languages = language ? new LanguageStore(new MemoryStorage(), ['pt-PT']) : undefined
  const result = renderApp(path, { layout, languages })
  await waitFor(() => expect(document.querySelector('[data-dex], .pokedex-empty')).not.toBeNull())
  return result
}

describe('Pokédex list (phone)', () => {
  it('shows a line per released species, in Pokédex order, with its numbers', async () => {
    await openPokedex()

    expect(shownDexes()).toEqual([
      6, 25, 26, 59, 122, 131, 146, 150, 151, 157, 382, 445, 487, 793, 899,
    ])
    expect(screen.getByText('15 Pokémon')).toBeInTheDocument()
    const charizard = within(line(6))
    expect(charizard.getByRole('link', { name: 'Charizard' })).toHaveAttribute(
      'href',
      '/pokedex/charizard',
    )
    expect(charizard.getByText('3,266')).toBeInTheDocument()
    expect(charizard.getByText('Fire')).toBeInTheDocument()
    expect(
      charizard.getByRole('button', { name: 'Show Mega Charizard Y on this line' }),
    ).toHaveTextContent('Mega Y')
    expect(within(line(122)).getByRole('link', { name: 'Mr. Mime' })).toHaveAttribute(
      'href',
      '/pokedex/mr-mime',
    )
  })

  it('switches a line to a version when its marker is tapped, and back', async () => {
    const user = userEvent.setup()
    await openPokedex()
    const charizard = within(line(6))

    await user.click(charizard.getByRole('button', { name: 'Show Mega Charizard Y on this line' }))
    expect(charizard.getByText('5,037')).toBeInTheDocument()
    expect(charizard.getByText('Showing Mega Charizard Y')).toBeInTheDocument()
    expect(charizard.getByRole('link', { name: 'Charizard' })).toHaveAttribute(
      'href',
      '/pokedex/charizard?version=charizard_mega_y',
    )

    await user.click(charizard.getByRole('button', { name: 'Show Mega Charizard Y on this line' }))
    expect(charizard.getByText('3,266')).toBeInTheDocument()
    expect(charizard.queryByText(/^Showing/)).not.toBeInTheDocument()
  })

  it('explains Shadow, Dynamax and G-Max instead of switching', async () => {
    const user = userEvent.setup()
    await openPokedex()
    const charizard = within(line(6))

    const shadow = charizard.getByRole('button', { name: /^Shadow: same stats/ })
    await user.click(shadow)
    expect(charizard.getByText(/deals 20% more damage/)).toBeInTheDocument()
    await user.click(shadow)
    expect(charizard.queryByText(/deals 20% more damage/)).not.toBeInTheDocument()
  })

  it('searches as you type, shows how it read the words, and clears', async () => {
    const user = userEvent.setup()
    await openPokedex()
    const box = screen.getByRole('searchbox', { name: 'Search the Pokédex' })

    await user.type(box, 'fire,water !legendary xyzzy')
    await waitFor(() => expect(shownDexes()).toEqual([]))
    expect(screen.getByText('Type: Fire')).toBeInTheDocument()
    expect(screen.getByText('or')).toBeInTheDocument()
    expect(screen.getByText('not Legendary')).toBeInTheDocument()
    expect(screen.getByText('“xyzzy”: not recognised')).toHaveAttribute('data-unknown')
    expect(screen.getByRole('heading', { name: 'No Pokémon match' })).toBeInTheDocument()
    expect(currentAddress()).toBe('/pokedex?q=fire%2Cwater+%21legendary+xyzzy')

    await user.click(screen.getByRole('button', { name: 'Clear the search' }))
    await waitFor(() => expect(shownDexes()).toHaveLength(15))
    expect(currentAddress()).toBe('/pokedex')
  })

  it('shows the best version for a stat in the search, and colours that stat', async () => {
    await openPokedex('/pokedex?q=fire%20atk230-')

    expect(shownDexes()).toEqual([6, 59, 146, 157])
    expect(within(line(6)).getByText('Showing Mega Charizard Y')).toBeInTheDocument()
    expect(within(line(6)).getByText('319')).toHaveAttribute('data-strong')
    expect(
      screen.getByText(/A Pokémon is shown when any of its versions matches/),
    ).toBeInTheDocument()
  })

  it('clears everything from the empty list', async () => {
    const user = userEvent.setup()
    await openPokedex('/pokedex?q=xyzzy&type=fire')

    await user.click(screen.getByRole('button', { name: 'Clear search and filters' }))
    expect(currentAddress()).toBe('/pokedex')
  })

  it('turns “Released” off to show unreleased Pokémon, marked as such', async () => {
    const user = userEvent.setup()
    await openPokedex()

    await user.click(screen.getByRole('button', { name: 'Released', pressed: true }))
    expect(currentAddress()).toBe('/pokedex?released=all')
    const arceus = within(line(493))
    expect(arceus.getByText('Not in GO yet')).toBeInTheDocument()
    expect(arceus.getByRole('link', { name: '5 forms' })).toHaveAttribute('href', '/pokedex/arceus')
  })

  it('toggles Has Mega and Has Shadow', async () => {
    const user = userEvent.setup()
    await openPokedex()

    await user.click(screen.getByRole('button', { name: 'Has Mega' }))
    expect(shownDexes()).toEqual([6, 382, 445])
    await user.click(screen.getByRole('button', { name: 'Has Shadow' }))
    expect(currentAddress()).toBe('/pokedex?has=mega%2Cshadow')
    await user.click(screen.getByRole('button', { name: 'Has Mega', pressed: true }))
    expect(currentAddress()).toBe('/pokedex?has=shadow')
  })

  it('sorts from the sort sheet; the chosen column again reverses it', async () => {
    const user = userEvent.setup()
    await openPokedex()

    await user.click(screen.getByRole('button', { name: /^Sort: Number/ }))
    await user.click(
      within(screen.getByRole('dialog', { name: 'Sort by' })).getByRole('button', {
        name: 'Attack',
      }),
    )
    expect(currentAddress()).toBe('/pokedex?sort=atk')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(shownDexes().slice(0, 2)).toEqual([150, 382])

    await user.click(screen.getByRole('button', { name: /^Sort: Attack ↓/ }))
    const dialog = screen.getByRole('dialog', { name: 'Sort by' })
    expect(within(dialog).getByText(/sorted from highest/)).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: /Attack/ }))
    expect(currentAddress()).toBe('/pokedex?sort=atk&order=asc')
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('explains the search and tries an example', async () => {
    const user = userEvent.setup()
    await openPokedex()

    await user.click(screen.getByRole('button', { name: 'How search works' }))
    const help = screen.getByRole('dialog', { name: 'How search works' })
    expect(within(help).getByText('A comma means either/or')).toBeInTheDocument()
    await user.click(within(help).getByRole('button', { name: 'fire,water,rock' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('searchbox')).toHaveValue('fire,water,rock')
    await waitFor(() => expect(shownDexes()).toEqual([6, 59, 131, 146, 157, 382, 793]))
  })
})

describe('the filter sheet', () => {
  it('counts as you pick, and applies with Show', async () => {
    const user = userEvent.setup()
    await openPokedex()

    await user.click(screen.getByRole('button', { name: 'Filters' }))
    const sheet = screen.getByRole('dialog', { name: 'Filters' })
    await user.click(within(sheet).getByRole('button', { name: 'Fire' }))
    expect(within(sheet).getByRole('button', { name: 'Show 4 Pokémon' })).toBeInTheDocument()
    await user.click(within(sheet).getByRole('button', { name: 'Flying' }))
    await user.click(within(sheet).getByRole('button', { name: 'Dragon' }))
    // A third type replaces the first: Flying + Dragon.
    expect(within(sheet).getByRole('button', { name: 'Fire', pressed: false })).toBeInTheDocument()
    await user.click(within(sheet).getByRole('button', { name: 'Dragon', pressed: true }))
    await user.click(within(sheet).getByRole('button', { name: 'Kanto' }))
    await user.click(within(sheet).getByRole('button', { name: 'Johto' }))
    await user.click(within(sheet).getByRole('button', { name: 'Johto', pressed: true }))
    await user.click(within(sheet).getByRole('button', { name: 'Legendary' }))
    await user.click(within(sheet).getByRole('button', { name: 'Has Shadow' }))
    await user.click(within(sheet).getByRole('button', { name: 'Released only', pressed: true }))
    await user.click(within(sheet).getByRole('button', { name: 'Show 1 Pokémon' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(currentAddress()).toBe(
      '/pokedex?type=flying&gen=1&has=shadow&cat=legendary&released=all',
    )
    expect(shownDexes()).toEqual([146])
    expect(screen.getByRole('button', { name: 'Filters · 4' })).toBeInTheDocument()
  })

  it('sets stat ranges with the sliders', async () => {
    const user = userEvent.setup()
    await openPokedex()

    await user.click(screen.getByRole('button', { name: 'Filters' }))
    const sheet = screen.getByRole('dialog', { name: 'Filters' })
    fireEvent.change(within(sheet).getByRole('slider', { name: 'Lowest Attack' }), {
      target: { value: '300' },
    })
    fireEvent.change(within(sheet).getByRole('slider', { name: 'Highest Max CP (L50)' }), {
      target: { value: '5000' },
    })
    // Thumbs can't cross: the lowest can't go above the highest.
    fireEvent.change(within(sheet).getByRole('slider', { name: 'Lowest Defense' }), {
      target: { value: '400' },
    })
    fireEvent.change(within(sheet).getByRole('slider', { name: 'Lowest Defense' }), {
      target: { value: '96' },
    })
    fireEvent.change(within(sheet).getByRole('slider', { name: 'Highest Stamina' }), {
      target: { value: '50' },
    })
    fireEvent.change(within(sheet).getByRole('slider', { name: 'Highest Stamina' }), {
      target: { value: '330' },
    })
    await user.click(within(sheet).getByRole('button', { name: /^Show/ }))

    expect(currentAddress()).toBe('/pokedex?atk=300-&cp=-5000')
    expect(shownDexes()).toEqual([150])
  })

  it('resets to what was applied, clears everything, and closes without applying', async () => {
    const user = userEvent.setup()
    await openPokedex('/pokedex?type=fire&q=kanto')

    await user.click(screen.getByRole('button', { name: 'Filters · 1' }))
    let sheet = screen.getByRole('dialog', { name: 'Filters' })
    await user.click(within(sheet).getByRole('button', { name: 'Clear all' }))
    expect(within(sheet).getByRole('button', { name: 'Fire', pressed: false })).toBeInTheDocument()
    await user.click(within(sheet).getByRole('button', { name: 'Reset' }))
    expect(within(sheet).getByRole('button', { name: 'Fire', pressed: true })).toBeInTheDocument()
    await user.click(within(sheet).getByRole('button', { name: 'Clear all' }))
    await user.click(within(sheet).getByRole('button', { name: 'All' }))
    await user.click(within(sheet).getByRole('button', { name: 'Close' }))
    expect(currentAddress()).toBe('/pokedex?type=fire&q=kanto')

    await user.click(screen.getByRole('button', { name: 'Filters · 1' }))
    sheet = screen.getByRole('dialog', { name: 'Filters' })
    await user.click(within(sheet).getByRole('button', { name: 'Clear all' }))
    await user.click(within(sheet).getByRole('button', { name: 'Show 9 Pokémon' }))
    expect(currentAddress()).toBe('/pokedex?q=kanto')
  })

  it('closes on a tap outside it', async () => {
    const user = userEvent.setup()
    await openPokedex()

    await user.click(screen.getByRole('button', { name: 'Filters' }))
    await user.click(document.querySelector('.sheet-overlay')!)
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})

describe('Pokédex list (desktop)', () => {
  it('shows a table whose headers sort, and the count next to the title', async () => {
    const user = userEvent.setup()
    await openPokedex('/pokedex', 'desktop')

    expect(screen.getByText('15 Pokémon')).toBeInTheDocument()
    const number = screen.getByRole('columnheader', { name: /#/ })
    expect(number).toHaveAttribute('aria-sort', 'ascending')
    await user.click(within(number).getByRole('button'))
    expect(currentAddress()).toBe('/pokedex?order=desc')
    await user.click(
      within(screen.getByRole('columnheader', { name: /Max CP/ })).getByRole('button'),
    )
    expect(currentAddress()).toBe('/pokedex?sort=cp')
    expect(screen.getByRole('columnheader', { name: /Max CP/ })).toHaveAttribute(
      'aria-sort',
      'descending',
    )
    await user.click(
      within(screen.getByRole('columnheader', { name: /Pokémon/ })).getByRole('button'),
    )
    expect(shownDexes().slice(0, 3)).toEqual([59, 6, 445])
  })

  it('switches a line while the pointer is on a marker', async () => {
    await openPokedex('/pokedex', 'desktop')
    const charizard = within(line(6))
    const megaX = charizard.getByRole('button', { name: 'Show Mega Charizard X on this line' })

    fireEvent.pointerEnter(megaX, { pointerType: 'mouse' })
    expect(charizard.getByText('Showing Mega Charizard X')).toBeInTheDocument()
    expect(charizard.getByText('Dragon')).toBeInTheDocument()
    fireEvent.pointerLeave(megaX, { pointerType: 'mouse' })
    expect(charizard.queryByText(/^Showing/)).not.toBeInTheDocument()

    // A touch on a touch screen isn't a hover.
    fireEvent.pointerEnter(megaX, { pointerType: 'touch' })
    fireEvent.pointerLeave(megaX, { pointerType: 'touch' })
    expect(charizard.queryByText(/^Showing/)).not.toBeInTheDocument()
  })

  it('shows set filters as chips that remove them', async () => {
    const user = userEvent.setup()
    await openPokedex(
      '/pokedex?atk=200-&def=-200&sta=150-300&cp=4000&has=dynamax,mega&cat=legendary',
      'desktop',
    )

    const labels = [
      'Attack ≥ 200',
      'Defense ≤ 200',
      'Stamina 150–300',
      'Max CP (L50) = 4,000',
      'Can Dynamax',
      'Legendary',
    ]
    for (const label of labels) {
      expect(screen.getByRole('button', { name: `Remove ${label}` })).toBeInTheDocument()
    }
    expect(screen.getByRole('button', { name: 'More filters · 7' })).toBeInTheDocument()
    for (const label of labels) {
      await user.click(screen.getByRole('button', { name: `Remove ${label}` }))
    }
    expect(currentAddress()).toBe('/pokedex?has=mega')
  })

  it('Type and Generation change only themselves, right under their chip', async () => {
    const user = userEvent.setup()
    await openPokedex('/pokedex?type=fire', 'desktop')

    const type = screen.getByRole('button', { name: /^Type: Fire/ })
    expect(type).toHaveAttribute('aria-expanded', 'false')
    await user.click(type)
    const typePanel = screen.getByRole('dialog', { name: 'Type' })
    await user.click(within(typePanel).getByRole('button', { name: 'Flying' }))
    expect(currentAddress()).toBe('/pokedex?type=fire%2Cflying')
    expect(screen.getByRole('button', { name: /^Type: Fire \+ Flying/ })).toBeInTheDocument()
    expect(shownDexes()).toEqual([6, 146])
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^Type: Fire \+ Flying/ })).toHaveFocus()

    await user.click(screen.getByRole('button', { name: /^Generation: All/ }))
    const generation = screen.getByRole('dialog', { name: 'Generation' })
    await user.click(within(generation).getByRole('button', { name: 'Kanto' }))
    await user.click(within(generation).getByRole('button', { name: 'Johto' }))
    expect(currentAddress()).toBe('/pokedex?type=fire%2Cflying&gen=1%2C2')
    expect(screen.getByRole('button', { name: /^Generation: Kanto, Johto/ })).toBeInTheDocument()
    await user.click(within(generation).getByRole('button', { name: 'Johto', pressed: true }))
    await user.click(within(generation).getByRole('button', { name: 'All' }))
    expect(currentAddress()).toBe('/pokedex?type=fire%2Cflying')
    // A click outside the panel closes it.
    await user.click(screen.getByRole('heading', { level: 1 }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    // The chip again closes it too.
    await user.click(screen.getByRole('button', { name: /^Generation: All/ }))
    await user.click(screen.getByRole('button', { name: /^Generation: All/ }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('“More filters” holds the rest, without Type and Generation', async () => {
    const user = userEvent.setup()
    await openPokedex('/pokedex?type=fire&gen=1&has=dynamax', 'desktop')

    await user.click(screen.getByRole('button', { name: 'More filters · 1' }))
    const sheet = screen.getByRole('dialog', { name: 'More filters' })
    expect(within(sheet).queryByRole('heading', { name: 'Type' })).not.toBeInTheDocument()
    expect(within(sheet).queryByRole('heading', { name: 'Generation' })).not.toBeInTheDocument()
    await user.click(within(sheet).getByRole('button', { name: 'Clear all' }))
    await user.click(within(sheet).getByRole('button', { name: /^Show/ }))
    // Clear all here keeps the type and generation chosen in the bar.
    expect(currentAddress()).toBe('/pokedex?type=fire&gen=1')
  })
})

describe('Pokédex list in Portuguese', () => {
  it('shows the game’s Portuguese names and texts', async () => {
    const user = userEvent.setup()
    await openPokedex('/pokedex?q=regional', 'phone', 'pt-PT')
    await user.clear(screen.getByRole('searchbox', { name: 'Pesquisar na Pokédex' }))
    await user.type(screen.getByRole('searchbox'), 'raichu de alola')

    await waitFor(() => expect(shownDexes()).toEqual([26]))
    const raichu = within(line(26))
    expect(
      raichu.getByRole('button', { name: 'Mostrar Raichu de Alola nesta linha' }),
    ).toHaveTextContent('Alola')
    expect(raichu.getByText('A mostrar Raichu de Alola')).toBeInTheDocument()
    expect(screen.getByText('1 Pokémon')).toBeInTheDocument()
  })
})

describe('when the Pokédex can’t be loaded', () => {
  it('says so and tries again', async () => {
    const user = userEvent.setup()
    answer = () => Promise.resolve(json({ title: 'Internal Server Error', status: 500 }, 500))
    renderApp('/pokedex')

    await screen.findByRole('heading', { name: 'This couldn’t be loaded' })
    answer = () => Promise.resolve(json(samplePokedex))
    await user.click(screen.getByRole('button', { name: 'Try again' }))
    await screen.findAllByText(/^#\d{3}$/)
  })

  it('says it’s loading meanwhile', () => {
    answer = () => new Promise(() => {})
    renderApp('/pokedex')

    expect(screen.getByRole('status')).toHaveTextContent('Loading the Pokédex…')
    act(() => {})
  })
})
