import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { readEnvironment } from '../environment'
import { admin, currentAddress, renderApp } from '../testing/app'
import { ash, testSession } from '../testing/auth'

beforeEach(() => vi.stubGlobal('fetch', () => new Promise(() => {})))
afterEach(() => vi.unstubAllGlobals())

// Every screen's address (decision 2026-11-01) → the screen it opens: its "Coming soon" heading and code.
const screens: [string, string, string][] = [
  ['/calendar', 'Calendar', 'A2'],
  ['/calendar/community-day-gible-2026-11-07', 'Event details', 'A3'],
  ['/eggs', 'Eggs & research', 'A4'],
  ['/pokedex?type=fire&atk=230', 'Pokédex', 'B1'],
  ['/pokedex/garchomp?version=mega', 'Pokémon', 'B2'],
  ['/pokedex/moves?kind=charged&type=ground', 'Moves', 'B3'],
  ['/pokedex/moves/earth-power', 'Move', 'B4'],
  ['/pokedex/types', 'Type chart', 'B5'],
  ['/battle/raids', 'Raid bosses', 'C1'],
  ['/battle/raids/mega-gengar', 'Raid boss', 'C2'],
  ['/battle/tiers/great-league', 'Tier lists', 'C3'],
  ['/battle/tiers/raids/ground', 'Tier lists', 'C3'],
  ['/battle/tiers/raids', 'Tier lists', 'C3'],
  ['/battle/teams?boss=kyogre', 'Best team', 'C4'],
  ['/filters', 'Keeper filters', 'D1'],
  ['/filters/builder?saved=7', 'Filter builder', 'D2'],
  ['/filters/saved', 'Saved filters', 'D3'],
  ['/collection', 'My collection', 'E1'],
  ['/collection/upload', 'Upload', 'E2'],
  ['/collection/imports/42', 'Import report', 'E2'],
  ['/collection/imports/42/changes', 'All changes', 'E2'],
  ['/admin', 'Data status', 'G1'],
  ['/admin/entries', 'Fix fetched entries', 'G2'],
]

describe('routes', () => {
  describe.each(['phone', 'desktop'] as const)('on a %s', (layout) => {
    it.each(screens)('%s opens %s (%s)', (path, name, code) => {
      renderApp(path, { layout, user: admin })

      const card = screen.getByRole('region', { name })
      expect(card).toHaveTextContent(`Screen ${code}`)
      expect(card).toHaveTextContent('Coming soon')
    })
  })

  it('shows what the address points at', () => {
    renderApp('/pokedex/garchomp')

    expect(screen.getByRole('region', { name: 'Pokémon' })).toHaveTextContent('garchomp')
  })

  it.each([
    ['/battle', '/battle/raids'],
    ['/battle/tiers', '/battle/tiers/great-league'],
  ])('%s opens %s', async (from, to) => {
    renderApp(from)

    await waitFor(() => expect(currentAddress()).toBe(to))
  })

  it('an unknown address shows "Page not found" with a way to Today', async () => {
    renderApp('/no/such/page')

    const notFound = screen.getByRole('region', { name: 'Page not found' })
    await userEvent.click(within(notFound).getByRole('link', { name: 'Go to Today' }))

    expect(currentAddress()).toBe('/')
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Today')
  })

  describe('personal screens', () => {
    it.each([
      ['/collection', 'Your collection lives here'],
      ['/collection/upload', 'Your collection lives here'],
      ['/collection/imports/42/changes', 'Your collection lives here'],
      ['/filters/saved', 'Keep your filters'],
    ])('%s signed out: the sign-in prompt in its place', async (path, prompt) => {
      renderApp(path)

      expect(await screen.findByRole('region', { name: prompt })).toBeInTheDocument()
      expect(screen.queryByText('Coming soon')).not.toBeInTheDocument()
    })

    it('shows nothing while the sign-in is still being checked', () => {
      const { session } = testSession()
      session.start = () => new Promise(() => {})
      renderApp('/collection', { session })

      expect(screen.queryByRole('region')).not.toBeInTheDocument()
    })
  })

  describe('admin screens', () => {
    it.each([
      ['signed out', undefined],
      ['signed in without the admin role', ash],
    ])('%s: "Page not found"', async (_, user) => {
      renderApp('/admin', { user })

      expect(await screen.findByRole('region', { name: 'Page not found' })).toBeInTheDocument()
      expect(screen.queryByText('Data status')).not.toBeInTheDocument()
    })

    it('shows nothing while the sign-in is still being checked', () => {
      const { session } = testSession()
      session.start = () => new Promise(() => {})
      renderApp('/admin/entries', { session })

      expect(screen.queryByRole('region')).not.toBeInTheDocument()
    })

    it('on a phone, Back returns to where Admin was opened from (no loop with Settings)', async () => {
      renderApp('/battle/raids', { user: { ...ash, role: 'SuperAdmin' } })

      await userEvent.click(screen.getByRole('button', { name: 'Account: Ash' }))
      await userEvent.click(screen.getByRole('menuitem', { name: 'Admin' }))
      expect(currentAddress()).toBe('/admin')
      expect(screen.queryByRole('navigation', { name: 'Tabs' })).not.toBeInTheDocument()

      await userEvent.click(screen.getByRole('button', { name: 'Back' }))

      await waitFor(() => expect(currentAddress()).toBe('/battle/raids'))
      expect(screen.getByRole('navigation', { name: 'Tabs' })).toBeInTheDocument()
    })

    it('opened directly, Back goes to Today', async () => {
      renderApp('/admin/entries', { user: admin })

      await userEvent.click(screen.getByRole('button', { name: 'Back' }))

      await waitFor(() => expect(currentAddress()).toBe('/'))
    })
  })

  describe('the component gallery (/design)', () => {
    it.each(['qa', 'local'])('is there on %s', (kind) => {
      renderApp('/design', { environment: readEnvironment({ VITE_APP_ENV: kind }) })

      expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Design system')
    })

    it('is not in production', () => {
      renderApp('/design')

      expect(screen.getByRole('region', { name: 'Page not found' })).toBeInTheDocument()
    })
  })

  describe('/sign-in', () => {
    it('opens the sign-in screen over Today; closing it leaves for Today', async () => {
      renderApp('/sign-in')

      const dialog = await screen.findByRole('dialog', { name: 'Sign in to make it yours' })
      expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Today')
      await userEvent.click(within(dialog).getByRole('button', { name: 'Not now, keep browsing' }))

      await waitFor(() => expect(currentAddress()).toBe('/'))
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })

    it('comes back to Today after Google', async () => {
      const leave = vi.fn()
      vi.stubGlobal('fetch', async () =>
        Response.json({ clientId: 'client', authorizationEndpoint: 'https://accounts.example' }),
      )
      renderApp('/sign-in', { navigate: leave })

      const dialog = await screen.findByRole('dialog')
      await userEvent.click(within(dialog).getByRole('button', { name: 'Continue with Google' }))

      await waitFor(() => expect(leave).toHaveBeenCalled())
      expect(JSON.parse(sessionStorage.getItem('mpgc.pendingSignIn')!).returnTo).toBe('/')
    })

    it('signed in already: goes to Today', async () => {
      renderApp('/sign-in', { user: ash })

      await waitFor(() => expect(currentAddress()).toBe('/'))
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })
  })

  it('"All changes" goes back to its import report', () => {
    renderApp('/collection/imports/42/changes', { user: ash })

    expect(screen.getByRole('link', { name: 'Report' })).toHaveAttribute(
      'href',
      '/collection/imports/42',
    )
  })

  it('signing out on a personal screen swaps it for the prompt', async () => {
    const { session } = testSession()
    renderApp('/collection', { session, user: ash })
    expect(screen.getByRole('region', { name: 'My collection' })).toBeInTheDocument()

    await act(() => session.signOut())

    expect(screen.getByRole('region', { name: 'Your collection lives here' })).toBeInTheDocument()
  })
})
