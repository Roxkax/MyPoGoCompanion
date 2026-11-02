import { act, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { readEnvironment } from '../environment'
import { LanguageStore } from '../i18n/language'
import { ash, MemoryStorage, routeFetch } from '../testing/auth'
import { renderApp } from '../testing/app'
import type { DataFreshness, Freshness } from './freshness'
import { overrideKey } from './freshness'

afterEach(() => {
  vi.unstubAllGlobals()
  window.sessionStorage.clear()
})

const fresh = (limitDays: number): DataFreshness => ({
  lastUpdatedAt: '2026-11-01T06:04:00+00:00',
  daysSinceUpdate: 0,
  outOfDate: false,
  limitDays,
})

const old = (days: number, limitDays: number): DataFreshness => ({
  lastUpdatedAt: '2026-10-20T18:00:00+00:00',
  daysSinceUpdate: days,
  outOfDate: true,
  limitDays,
})

const liveText = /Raids and events haven’t updated in 12 days\. Some may have changed in the game\./
const gameText = /Game data hasn’t updated in 4 days\. Recent changes in the game may be missing\./

function answer(freshness: Freshness) {
  const calls: string[] = []
  vi.stubGlobal(
    'fetch',
    routeFetch({
      '/freshness': () => {
        calls.push('/freshness')
        return Response.json(freshness)
      },
    }),
  )
  return calls
}

const bothOld: Freshness = { gameData: old(4, 3), liveData: old(12, 9) }

function bars() {
  return screen.queryAllByText(/haven’t updated|hasn’t updated/).map((bar) => bar.textContent)
}

describe('the out-of-date warning', () => {
  it.each(['phone', 'desktop'] as const)(
    'shows only the kinds a screen uses (%s)',
    async (layout) => {
      answer(bothOld)
      renderApp('/', { layout })

      expect(await screen.findByText(liveText)).toBeInTheDocument()
      expect(screen.queryByText(gameText)).not.toBeInTheDocument()
      expect(screen.getByRole('main')).toContainElement(screen.getByText(liveText))
    },
  )

  it('shows both on screens with both, raids and events first', async () => {
    answer(bothOld)
    renderApp('/battle/raids')

    await screen.findByText(liveText)
    expect(bars()).toEqual([expect.stringMatching(liveText), expect.stringMatching(gameText)])
  })

  it.each([
    ['/calendar', 'live'],
    ['/calendar/community-day', 'live'],
    ['/eggs', 'live'],
    ['/pokedex', 'game'],
    ['/pokedex/garchomp', 'game'],
    ['/pokedex/moves', 'game'],
    ['/pokedex/moves/earthquake', 'game'],
    ['/pokedex/types', 'game'],
    ['/battle/raids/kyogre', 'both'],
    ['/battle/tiers/great-league', 'game'],
    ['/battle/tiers/raids/dragon', 'game'],
    ['/battle/teams', 'both'],
    ['/filters', 'game'],
    ['/filters/builder', 'game'],
  ])('%s warns about %s data', async (path, kinds) => {
    answer(bothOld)
    renderApp(path)

    await screen.findByText(kinds === 'game' ? gameText : liveText)
    expect(bars()).toHaveLength(kinds === 'both' ? 2 : 1)
  })

  it.each(['/settings', '/filters/saved', '/sign-in', '/no-such-page'])(
    '%s never warns',
    async (path) => {
      const calls = answer(bothOld)
      renderApp(path)
      await act(() => Promise.resolve())

      expect(bars()).toEqual([])
      expect(calls).toEqual([])
    },
  )

  it('collection: not above the sign-in prompt when signed out', async () => {
    answer(bothOld)
    renderApp('/collection')
    expect(await screen.findByRole('button', { name: /Google/ })).toBeInTheDocument()

    expect(bars()).toEqual([])
  })

  it('collection: warns once signed in', async () => {
    answer(bothOld)
    renderApp('/collection', { user: ash })

    expect(await screen.findByText(gameText)).toBeInTheDocument()
  })

  it('says nothing while the data is up to date', async () => {
    const calls = answer({ gameData: fresh(3), liveData: fresh(9) })
    renderApp('/battle/raids')
    await vi.waitFor(() => expect(calls).toHaveLength(1))
    await act(() => Promise.resolve())

    expect(bars()).toEqual([])
  })

  it('says nothing when the API can’t be reached', async () => {
    vi.stubGlobal('fetch', () => Promise.reject(new TypeError('Failed to fetch')))
    renderApp('/battle/raids')
    await act(() => new Promise((resolve) => setTimeout(resolve, 20)))

    expect(bars()).toEqual([])
  })

  it('asks the API once for every screen', async () => {
    const calls = answer(bothOld)
    renderApp('/battle/raids')
    await screen.findByText(liveText)
    await act(async () => {
      window.history.pushState(null, '', '/pokedex')
      window.dispatchEvent(new PopStateEvent('popstate'))
      await Promise.resolve()
    })

    expect(await screen.findByText(gameText)).toBeInTheDocument()
    expect(calls).toEqual(['/freshness'])
  })

  it('counts one day in the singular', async () => {
    answer({ gameData: old(1, 1), liveData: old(1, 1) })
    renderApp('/battle/raids')

    expect(
      await screen.findByText(
        'Raids and events haven’t updated in 1 day. Some may have changed in the game.',
      ),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'Game data hasn’t updated in 1 day. Recent changes in the game may be missing.',
      ),
    ).toBeInTheDocument()
  })

  it('speaks Portuguese', async () => {
    answer(bothOld)
    renderApp('/battle/raids', { languages: new LanguageStore(new MemoryStorage(), ['pt-PT']) })

    expect(
      await screen.findByText(
        'Os reides e eventos não são atualizados há 12 dias. Alguns podem ter mudado no jogo.',
      ),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'Os dados do jogo não são atualizados há 4 dias. Podem faltar mudanças recentes no jogo.',
      ),
    ).toBeInTheDocument()
  })
})

describe('pretending data is old (?stale=, QA and local only)', () => {
  const qa = readEnvironment({ VITE_APP_ENV: 'qa' })

  it('shows the asked kinds on QA and keeps them while moving around', async () => {
    answer({ gameData: fresh(3), liveData: fresh(9) })
    renderApp('/?stale=both', { environment: qa })

    expect(await screen.findByText(liveText)).toBeInTheDocument()
    expect(window.sessionStorage.getItem(overrideKey)).toBe('both')
  })

  it('remembers it for the tab and ends with ?stale=off', async () => {
    answer({ gameData: fresh(3), liveData: fresh(9) })
    window.sessionStorage.setItem(overrideKey, 'game')
    renderApp('/pokedex', { environment: qa })
    expect(await screen.findByText(gameText)).toBeInTheDocument()

    await act(async () => {
      window.history.pushState(null, '', '/pokedex?stale=off')
      window.dispatchEvent(new PopStateEvent('popstate'))
      await Promise.resolve()
    })
    expect(bars()).toEqual([])
    expect(window.sessionStorage.getItem(overrideKey)).toBeNull()
  })

  it('does nothing in production', async () => {
    const calls = answer({ gameData: fresh(3), liveData: fresh(9) })
    renderApp('/battle/raids?stale=both')
    await vi.waitFor(() => expect(calls).toHaveLength(1))
    await act(() => Promise.resolve())

    expect(bars()).toEqual([])
    expect(window.sessionStorage.getItem(overrideKey)).toBeNull()
  })

  it('the design gallery shows both bars', async () => {
    answer({ gameData: fresh(3), liveData: fresh(9) })
    renderApp('/design', { environment: qa })

    const gallery = (await screen.findByRole('heading', { name: 'Out-of-date warnings' }))
      .parentElement!
    expect(within(gallery).getByText(liveText)).toBeInTheDocument()
    expect(within(gallery).getByText(gameText)).toBeInTheDocument()
  })
})
