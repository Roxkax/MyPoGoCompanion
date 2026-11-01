import { act, fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { admin, currentAddress, renderApp } from '../testing/app'
import { ash } from '../testing/auth'

beforeEach(() => vi.stubGlobal('fetch', () => new Promise(() => {})))
afterEach(() => vi.unstubAllGlobals())

function tabBar() {
  return screen.getByRole('navigation', { name: 'Tabs' })
}

function current(nav: HTMLElement) {
  return within(nav)
    .getAllByRole('link')
    .filter((link) => link.getAttribute('aria-current') === 'page')
    .map((link) => link.textContent)
}

describe('phone layout', () => {
  it('has the five tabs in the bottom tab bar, the current one lit', () => {
    renderApp('/battle/raids/kyogre')

    expect(
      within(tabBar())
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual(['Today', 'Pokédex', 'Battle', 'Filters', 'Collection'])
    expect(current(tabBar())).toEqual(['Battle'])
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument()
  })

  it('tapping a tab opens it', async () => {
    renderApp('/')

    await userEvent.click(within(tabBar()).getByRole('link', { name: 'Filters' }))

    expect(currentAddress()).toBe('/filters')
    expect(current(tabBar())).toEqual(['Filters'])
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Filters')
  })

  it('a tab screen: large title, Ask, profile and its section switcher', () => {
    renderApp('/pokedex/moves')

    const header = screen.getAllByRole('banner')[0]!
    expect(within(header).getByRole('heading', { level: 1 })).toHaveTextContent('Pokédex')
    expect(within(header).getByRole('button', { name: 'Ask' })).toBeInTheDocument()
    expect(within(header).getByRole('button', { name: 'Profile' })).toBeInTheDocument()
    const sections = within(header).getByRole('navigation', { name: 'Pokédex sections' })
    expect(current(sections)).toEqual(['Moves'])
  })

  it('Today: date, calendar button and search bar', async () => {
    renderApp('/')

    expect(
      screen.getByRole('button', { name: /Search Pokémon, moves, bosses/ }),
    ).toBeInTheDocument()
    await userEvent.click(screen.getByRole('link', { name: 'Calendar' }))
    expect(currentAddress()).toBe('/calendar')
  })

  it('a detail screen: back to its parent screen, its name and Ask', async () => {
    renderApp('/calendar/community-day-gible-2026-11-07')

    const header = screen.getAllByRole('banner')[0]!
    expect(within(header).getByRole('heading', { level: 1 })).toHaveTextContent('Event')
    expect(within(header).getByRole('button', { name: 'Ask' })).toBeInTheDocument()
    await userEvent.click(within(header).getByRole('link', { name: 'Calendar' }))
    expect(currentAddress()).toBe('/calendar')
  })

  it('a Pokémon page has no name in the header (the page shows it)', () => {
    renderApp('/pokedex/garchomp')

    const header = screen.getAllByRole('banner')[0]!
    expect(within(header).queryByRole('heading')).not.toBeInTheDocument()
    expect(within(header).getByRole('link', { name: 'Pokédex' })).toHaveAttribute(
      'href',
      '/pokedex',
    )
  })

  it('Settings and Admin have no tab bar', () => {
    renderApp('/settings')
    expect(screen.queryByRole('navigation', { name: 'Tabs' })).not.toBeInTheDocument()
  })
})

describe('desktop layout', () => {
  function sidebar() {
    return screen.getByRole('complementary')
  }

  it('sidebar: the tabs, quick links and Settings; top bar: search, Ask and profile', () => {
    renderApp('/', { layout: 'desktop' })

    expect(
      within(sidebar())
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual([
      'MyPoGoCompanion',
      'Today',
      'Pokédex',
      'Battle',
      'Filters',
      'Collection',
      'Calendar',
      'Eggs & research',
      'Moves',
      'Tier lists',
      'Settings',
    ])
    const topBar = screen.getByRole('banner')
    expect(within(topBar).getByRole('button', { name: /Search Pokémon, moves, bosses, events/ }))
    expect(within(topBar).getByRole('button', { name: 'Ask' })).toBeInTheDocument()
    expect(within(topBar).getByRole('button', { name: 'Profile' })).toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Tabs' })).not.toBeInTheDocument()
  })

  it.each([
    ['/', 'Today'],
    ['/calendar', 'Calendar'],
    ['/pokedex/moves/earth-power', 'Moves'],
    ['/battle/tiers/great-league', 'Tier lists'],
    ['/settings', 'Settings'],
  ])('%s lights %s only', (path, item) => {
    renderApp(path, { layout: 'desktop' })

    expect(current(sidebar())).toEqual([item])
  })

  it('Admin shows for admins only', () => {
    renderApp('/', { layout: 'desktop', user: ash })
    expect(within(sidebar()).queryByRole('link', { name: 'Admin' })).not.toBeInTheDocument()
  })

  it('Admin is at the bottom for an admin, and lit on the admin screens', () => {
    renderApp('/admin/entries', { layout: 'desktop', user: admin })

    expect(current(sidebar())).toEqual(['Admin'])
  })

  it('the page title, with the section switcher where the design has one', () => {
    renderApp('/battle/teams', { layout: 'desktop' })

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Battle')
    expect(current(screen.getByRole('navigation', { name: 'Battle sections' }))).toEqual(['Teams'])
  })

  it('the Pokédex sections are sidebar links instead of a switcher', () => {
    renderApp('/pokedex/types', { layout: 'desktop' })

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Type chart')
    expect(screen.queryByRole('navigation', { name: 'Pokédex sections' })).not.toBeInTheDocument()
  })

  it('detail screens: the back link above the title', () => {
    renderApp('/eggs', { layout: 'desktop' })

    const main = screen.getByRole('main')
    expect(within(main).getByRole('link', { name: 'Today' })).toHaveAttribute('href', '/')
    expect(within(main).getByRole('heading', { level: 1 })).toHaveTextContent('Eggs & research')
  })

  it('switches layout when the window is resized', () => {
    const { viewport } = renderApp('/pokedex')

    expect(screen.getByRole('navigation', { name: 'Tabs' })).toBeInTheDocument()
    viewport.resize('desktop')
    expect(screen.getByRole('complementary')).toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Tabs' })).not.toBeInTheDocument()
  })
})

describe('search and Ask', () => {
  it('Ask opens the AI chat over the page; signed out it asks to sign in', async () => {
    renderApp('/pokedex')

    await userEvent.click(screen.getByRole('button', { name: 'Ask' }))

    const panel = screen.getByRole('dialog', { name: 'Ask' })
    expect(within(panel).getByRole('region', { name: 'Ask the AI assistant' })).toBeInTheDocument()
    expect(currentAddress()).toBe('/pokedex')
  })

  it('signed in, Ask shows what is coming', async () => {
    renderApp('/', { user: ash })

    await userEvent.click(screen.getByRole('button', { name: 'Ask' }))

    expect(
      within(screen.getByRole('dialog')).getByText(
        'Screen H1 · comes with E11-F01 Chat that answers from our data',
      ),
    ).toBeInTheDocument()
  })

  it('the search bar opens search; Escape closes it', async () => {
    renderApp('/')

    await userEvent.click(screen.getByRole('button', { name: /Search Pokémon/ }))
    expect(screen.getByRole('dialog', { name: 'Search' })).toBeInTheDocument()

    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(currentAddress()).toBe('/')
  })

  it("the browser's Back button closes the panel instead of leaving the page", async () => {
    renderApp('/battle/raids', { layout: 'desktop' })

    await userEvent.click(screen.getByRole('button', { name: 'Ask' }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()

    act(() => window.history.back())

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(currentAddress()).toBe('/battle/raids')
  })

  it('Close, a click outside the panel, but not inside it', async () => {
    renderApp('/', { layout: 'desktop' })

    await userEvent.click(screen.getByRole('button', { name: 'Ask' }))
    await userEvent.click(screen.getByRole('dialog'))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Close' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())

    await userEvent.click(screen.getByRole('button', { name: 'Ask' }))
    fireEvent.click(document.querySelector('.overlay')!)
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('Ctrl K opens search from anywhere; going from Ask to search keeps one step to close', async () => {
    renderApp('/filters', { layout: 'desktop' })

    await userEvent.click(screen.getByRole('button', { name: 'Ask' }))
    await userEvent.keyboard('{Control>}k{/Control}')
    expect(screen.getByRole('dialog', { name: 'Search' })).toBeInTheDocument()
    await userEvent.keyboard('{Meta>}k{/Meta}')
    expect(screen.getByRole('dialog', { name: 'Search' })).toBeInTheDocument()

    act(() => window.history.back())
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(currentAddress()).toBe('/filters')
  })

  it('a panel open when the page loads closes without leaving the page', async () => {
    window.history.replaceState(
      { usr: { overlay: 'search' }, key: 'default', idx: 0 },
      '',
      '/pokedex',
    )
    renderAppKeepingHistory('/pokedex')

    expect(screen.getByRole('dialog', { name: 'Search' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Close' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(currentAddress()).toBe('/pokedex')
  })
})

/** renderApp, but keeping the history entry the test set up (with its saved panel). */
function renderAppKeepingHistory(path: string) {
  const state = window.history.state as unknown
  const replace = window.history.replaceState.bind(window.history)
  window.history.replaceState = (_data, unused, url) => replace(state, unused, url)
  try {
    return renderApp(path)
  } finally {
    window.history.replaceState = replace
  }
}

describe('back', () => {
  it('"Back" on Settings returns to the previous page', async () => {
    renderApp('/battle/raids')

    await userEvent.click(screen.getByRole('button', { name: 'Profile' }))
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Settings' }))
    expect(currentAddress()).toBe('/settings')

    await userEvent.click(screen.getByRole('button', { name: 'Back' }))
    await waitFor(() => expect(currentAddress()).toBe('/battle/raids'))
  })

  it('"Back" on Settings opened directly goes to Today', async () => {
    renderApp('/settings')

    await userEvent.click(screen.getByRole('button', { name: 'Back' }))

    await waitFor(() => expect(currentAddress()).toBe('/'))
  })
})
