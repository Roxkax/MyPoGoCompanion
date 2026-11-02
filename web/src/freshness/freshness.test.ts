import { describe, expect, it } from 'vitest'
import { MemoryStorage } from '../testing/auth'
import {
  applyOverride,
  overrideKey,
  pretendDays,
  readOverride,
  staleKinds,
  type DataFreshness,
  type Freshness,
} from './freshness'

const upToDate = (limitDays: number): DataFreshness => ({
  lastUpdatedAt: '2026-11-01T06:04:00+00:00',
  daysSinceUpdate: 0,
  outOfDate: false,
  limitDays,
})

const old = (days: number, limitDays: number): DataFreshness => ({
  lastUpdatedAt: '2026-10-20T06:04:00+00:00',
  daysSinceUpdate: days,
  outOfDate: true,
  limitDays,
})

const neverRun = (limitDays: number): DataFreshness => ({
  lastUpdatedAt: null,
  daysSinceUpdate: null,
  outOfDate: false,
  limitDays,
})

describe('staleKinds', () => {
  const bothOld: Freshness = { gameData: old(4, 3), liveData: old(12, 9) }

  it('lists only the kinds the screen shows, raids and events first', () => {
    expect(staleKinds(bothOld, ['game', 'live'])).toEqual([
      { kind: 'live', days: 12 },
      { kind: 'game', days: 4 },
    ])
    expect(staleKinds(bothOld, ['game'])).toEqual([{ kind: 'game', days: 4 }])
    expect(staleKinds(bothOld, ['live'])).toEqual([{ kind: 'live', days: 12 }])
    expect(staleKinds(bothOld, [])).toEqual([])
  })

  it('says nothing while data is up to date, never updated, or not known yet', () => {
    expect(staleKinds({ gameData: upToDate(3), liveData: upToDate(9) }, ['game', 'live'])).toEqual(
      [],
    )
    expect(staleKinds({ gameData: neverRun(3), liveData: neverRun(9) }, ['game', 'live'])).toEqual(
      [],
    )
    expect(staleKinds(undefined, ['game', 'live'])).toEqual([])
  })

  it('ignores an out-of-date flag without a day count', () => {
    const odd: Freshness = { gameData: { ...neverRun(3), outOfDate: true }, liveData: upToDate(9) }
    expect(staleKinds(odd, ['game'])).toEqual([])
  })
})

describe('readOverride', () => {
  it('is ignored in production, whatever the address or storage says', () => {
    const storage = new MemoryStorage()
    storage.setItem(overrideKey, 'both')

    expect(readOverride('production', '?stale=both', storage)).toBeNull()
    expect(storage.getItem(overrideKey)).toBe('both')
  })

  it.each(['raids', 'game', 'both'] as const)(
    'takes ?stale=%s on QA and keeps it for the tab',
    (value) => {
      const storage = new MemoryStorage()

      expect(readOverride('qa', `?stale=${value}`, storage)).toBe(value)
      expect(readOverride('qa', '', storage)).toBe(value)
      expect(readOverride('local', '?other=1', storage)).toBe(value)
    },
  )

  it('ends with ?stale=off (or any other value)', () => {
    const storage = new MemoryStorage()
    readOverride('qa', '?stale=both', storage)

    expect(readOverride('qa', '?stale=off', storage)).toBeNull()
    expect(readOverride('qa', '', storage)).toBeNull()
    readOverride('qa', '?stale=game', storage)
    expect(readOverride('qa', '?stale=', storage)).toBeNull()
    expect(storage.getItem(overrideKey)).toBeNull()
  })

  it('ignores a saved value it does not know', () => {
    const storage = new MemoryStorage()
    storage.setItem(overrideKey, 'everything')

    expect(readOverride('qa', '', storage)).toBeNull()
  })

  it('works from the address alone when storage is missing or blocked', () => {
    const blocked = {
      getItem: () => {
        throw new Error('blocked')
      },
      setItem: () => {
        throw new Error('blocked')
      },
      removeItem: () => {
        throw new Error('blocked')
      },
    }

    expect(readOverride('qa', '?stale=raids', blocked)).toBe('raids')
    expect(readOverride('qa', '?stale=off', blocked)).toBeNull()
    expect(readOverride('qa', '', blocked)).toBeNull()
    expect(readOverride('qa', '?stale=game', null)).toBe('game')
    expect(readOverride('qa', '', null)).toBeNull()
  })
})

describe('applyOverride', () => {
  const real: Freshness = { gameData: upToDate(3), liveData: upToDate(9) }

  it('leaves the API answer alone without an override', () => {
    expect(applyOverride(real, null)).toBe(real)
    expect(applyOverride(undefined, null)).toBeUndefined()
  })

  it('makes only the asked kinds old, keeping the real limits', () => {
    const raids = applyOverride({ gameData: upToDate(5), liveData: upToDate(14) }, 'raids')!
    expect(raids.liveData).toMatchObject({
      outOfDate: true,
      daysSinceUpdate: pretendDays.live,
      limitDays: 14,
    })
    expect(raids.gameData).toEqual(upToDate(5))

    const game = applyOverride(real, 'game')!
    expect(game.gameData).toMatchObject({ outOfDate: true, daysSinceUpdate: pretendDays.game })
    expect(game.liveData).toEqual(upToDate(9))

    const both = applyOverride(real, 'both')!
    expect(staleKinds(both, ['game', 'live'])).toEqual([
      { kind: 'live', days: 12 },
      { kind: 'game', days: 4 },
    ])
  })

  it('pretends even before the API has answered', () => {
    const both = applyOverride(undefined, 'both')!
    expect(both.gameData.limitDays).toBe(3)
    expect(both.liveData.limitDays).toBe(9)
    expect(applyOverride(undefined, 'raids')!.gameData.outOfDate).toBe(false)
  })
})
