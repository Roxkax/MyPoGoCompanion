// The out-of-date warning (spec 16.3, E01-F07): no "data as of" dates anywhere; only when data stops updating, an
// orange bar on the screens that show it. The API decides what is out of date (GET /freshness: 3 days without a daily
// game-data import, 9 days without a raids-and-events update; nothing is out of date before the first run).

import type { Schemas } from '../api/client'
import type { EnvironmentKind } from '../environmentKind'

/** What GET /freshness returns. */
export type Freshness = Schemas['FreshnessResponse']
export type DataFreshness = Schemas['DataFreshness']

/** The two kinds of data a screen can show: `game` (daily import) and `live` (raids and events, weekly). */
export type DataKind = 'game' | 'live'

/** The order the bars show in when a screen has both. */
export const dataKinds: readonly DataKind[] = ['live', 'game']

/** A kind that is out of date, with the whole days since its last update. */
export interface StaleData {
  kind: DataKind
  days: number
}

function entry(freshness: Freshness, kind: DataKind): DataFreshness {
  return kind === 'game' ? freshness.gameData : freshness.liveData
}

/** Which of these kinds are out of date, in {@link dataKinds} order. */
export function staleKinds(
  freshness: Freshness | undefined,
  kinds: readonly DataKind[],
): StaleData[] {
  if (!freshness) {
    return []
  }
  return dataKinds
    .filter((kind) => kinds.includes(kind))
    .flatMap((kind) => {
      const { outOfDate, daysSinceUpdate } = entry(freshness, kind)
      return outOfDate && daysSinceUpdate !== null ? [{ kind, days: daysSinceUpdate }] : []
    })
}

/*
 * QA and local only: `?stale=raids`, `?stale=game` or `?stale=both` makes the app act as if that data were old, so the
 * warning can be seen before the jobs exist (QA's database has no job runs yet). Kept for the browser tab's session so
 * it holds while moving between screens; `?stale=off` ends it. Production ignores it completely.
 */

export type StaleOverride = 'raids' | 'game' | 'both'

export const overrideKey = 'mpgc.stale'
export const overrideParam = 'stale'

type SimpleStorage = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>

function isOverride(value: string | null): value is StaleOverride {
  return value === 'raids' || value === 'game' || value === 'both'
}

/** The pretend-old setting for this page, saving or clearing it when the address asks. */
export function readOverride(
  kind: EnvironmentKind,
  search: string,
  storage: SimpleStorage | null,
): StaleOverride | null {
  if (kind === 'production') {
    return null
  }
  const asked = new URLSearchParams(search).get(overrideParam)
  try {
    if (isOverride(asked)) {
      storage?.setItem(overrideKey, asked)
      return asked
    }
    if (asked !== null) {
      storage?.removeItem(overrideKey)
      return null
    }
    const saved = storage?.getItem(overrideKey) ?? null
    return isOverride(saved) ? saved : null
  } catch {
    // Storage blocked: the address alone decides.
    return isOverride(asked) ? asked : null
  }
}

/** Days shown when pretending: past each limit, as in the design's example. */
export const pretendDays: Record<DataKind, number> = { game: 4, live: 12 }

/** The API's answer with the pretend-old kinds swapped in. */
export function applyOverride(
  freshness: Freshness | undefined,
  override: StaleOverride | null,
): Freshness | undefined {
  if (!override) {
    return freshness
  }
  const pretend = (kind: DataKind, on: boolean, real: DataFreshness | undefined): DataFreshness =>
    on
      ? {
          lastUpdatedAt: null,
          daysSinceUpdate: pretendDays[kind],
          outOfDate: true,
          limitDays: real?.limitDays ?? (kind === 'game' ? 3 : 9),
        }
      : (real ?? { lastUpdatedAt: null, daysSinceUpdate: null, outOfDate: false, limitDays: 0 })
  return {
    gameData: pretend('game', override !== 'raids', freshness?.gameData),
    liveData: pretend('live', override !== 'game', freshness?.liveData),
  }
}
