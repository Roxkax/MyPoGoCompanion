import type { Schemas } from '../api/client'
import { normalize } from '../pokedex/normalize'
import type { PokemonType } from '../ui/pokemonTypes'

// The Pokémon page's data (B2, E03-F02): one answer per species from GET /pokedex/{dex}, turned into what the page
// shows. Everything here is plain calculation, so it is tested on its own.

export type PokemonPage = Schemas['PokemonPageResponse']
export type PageForm = Schemas['PageForm']
export type PageMove = Schemas['PageMove']
export type PageAppearance = Schemas['PageAppearance']
export type PageEvolution = Schemas['PageEvolution']
export type FamilyMember = Schemas['FamilyMember']
export type League = Schemas['PagePvpRanking']['league']

export const leagues: readonly League[] = ['Great', 'Ultra', 'Master']

/** The CP caps of the leagues (Master League has none). */
export const leagueCaps: Record<League, number> = {
  Great: 1500,
  Ultra: 2500,
  Master: Number.MAX_SAFE_INTEGER,
}

/** The tier list's address of a league (`/battle/tiers/great-league`). */
export function leaguePath(league: League): string {
  return `/battle/tiers/${league.toLowerCase()}-league`
}

/**
 * A version the page can show: a form, or a form's Shadow (same stats, its own rankings). Its key is the address's
 * `?version=`: the form id, with `_shadow` for the Shadow (`garchomp_shadow`).
 */
export interface PageVersion {
  key: string
  form: PageForm
  shadow: boolean
}

/** The versions on the switcher, in the API's order: each form with its own stats, then its Shadow when released. */
export function versionsOf(page: PokemonPage): PageVersion[] {
  const list: PageVersion[] = []
  for (const form of page.forms) {
    if (form.looksOnly) continue
    list.push({ key: form.id, form, shadow: false })
    if (form.shadow) list.push({ key: `${form.id}_shadow`, form, shadow: true })
  }
  return list
}

/** The version the address asks for, else the main form (an unknown `?version=` is ignored). */
export function pickVersion(versions: PageVersion[], key: string | null): PageVersion | undefined {
  return versions.find((version) => version.key === key) ?? versions[0]
}

/** The species' number for an address name (`mr-mime`, `garchomp`), from the Pokédex list. */
export function dexForSlug(
  species: readonly { dex: number; nameEn: string }[],
  slug: string,
): number | null {
  const wanted = normalize(decodeURIComponent(slug)).replace(/ /g, '-')
  // An address with the number works too (`/pokedex/445`).
  if (/^\d+$/.test(wanted)) {
    const dex = Number(wanted)
    return species.some((s) => s.dex === dex) ? dex : null
  }
  return species.find((s) => normalize(s.nameEn).replace(/ /g, '-') === wanted)?.dex ?? null
}

/** The bars an IV falls in on the appraisal screen: 0 → 0, 1–5 → 1, 6–10 → 2, 11–14 → 3, 15 → 4. */
const barLow = [0, 1, 6, 11, 15] as const
const barHigh = [0, 5, 10, 14, 15] as const

/** The IVs a range of bars keeps (0–1 bars → 0–5). */
export function ivsOfBars(min: number, max: number): [number, number] {
  return [barLow[min] ?? 0, barHigh[max] ?? 15]
}

/** The keeper bars of a league as IV ranges: [attack, defense, HP]. */
export function keeperRanges(
  box: Schemas['PageKeeperBox'],
): [[number, number], [number, number], [number, number]] {
  return [
    ivsOfBars(box.attackMin, box.attackMax),
    ivsOfBars(box.defenseMin, box.defenseMax),
    ivsOfBars(box.staminaMin, box.staminaMax),
  ]
}

// ---- IV rank checker (the engine's LeagueStats, in the browser) ----

export interface Stats {
  attack: number
  defense: number
  stamina: number
}

export interface Spread {
  attack: number
  defense: number
  stamina: number
}

/** A spread in a league: the highest level under the cap, its CP and stat product. */
export interface SpreadInLeague {
  level: number
  cp: number
  statProduct: number
}

/** What the checker shows for one spread in one league. */
export interface IvCheck extends SpreadInLeague {
  rank: number
  /** Its stat product as a share of rank 1's (1 = rank 1). */
  share: number
}

const minimumCp = 10
/** Leagues stop at level 50 (best buddy's 51 doesn't count), as in the engine. */
const maxLevel = 50

function levelAt(index: number): number {
  return 1 + index / 2
}

/** CP from the stats with IVs added, as the game rounds it. */
export function cpOf(attack: number, defense: number, stamina: number, cpm: number): number {
  return Math.max(minimumCp, Math.floor((attack * Math.sqrt(defense * stamina) * cpm * cpm) / 10))
}

/** All 4,096 spreads in a league (index = attack × 256 + defense × 16 + HP), like the engine's LeagueStats. */
export function leagueSpreads(
  stats: Stats,
  cap: number,
  multipliers: readonly number[],
): SpreadInLeague[] {
  const levels = Math.min(multipliers.length, (maxLevel - 1) * 2 + 1)
  const result: SpreadInLeague[] = new Array<SpreadInLeague>(4096)
  for (let index = 0; index < 4096; index++) {
    const a = stats.attack + Math.floor(index / 256)
    const d = stats.defense + (Math.floor(index / 16) % 16)
    const h = stats.stamina + (index % 16)
    const root = Math.sqrt(d * h)
    const cpAt = (level: number) => {
      const m = multipliers[level] ?? 0
      return Math.max(minimumCp, Math.floor((a * root * (m * m)) / 10))
    }
    let levelIndex = 0
    if (cpAt(0) <= cap) {
      let low = 0
      let high = levels - 1
      while (low < high) {
        const mid = Math.floor((low + high + 1) / 2)
        if (cpAt(mid) <= cap) low = mid
        else high = mid - 1
      }
      levelIndex = low
    }
    const m = multipliers[levelIndex] ?? 0
    result[index] = {
      level: levelAt(levelIndex),
      cp: cpAt(levelIndex),
      statProduct: a * m * (d * m) * Math.max(Math.floor(h * m), minimumCp),
    }
  }
  return result
}

/** One spread's rank (equal stat products share a rank), share of rank 1, level and CP in a league. */
export function checkSpread(spreads: readonly SpreadInLeague[], ivs: Spread): IvCheck {
  const index = ivs.attack * 256 + ivs.defense * 16 + ivs.stamina
  const mine = spreads[index]
  if (!mine) throw new RangeError('IVs go from 0 to 15')
  let higher = 0
  let best = 0
  for (const spread of spreads) {
    if (spread.statProduct > mine.statProduct) higher++
    if (spread.statProduct > best) best = spread.statProduct
  }
  return { ...mine, rank: higher + 1, share: best > 0 ? mine.statProduct / best : 0 }
}

/** An IV typed in the checker, kept between 0 and 15 (empty or not a number → 0). */
export function clampIv(text: string): number {
  const value = Number.parseInt(text, 10)
  return Number.isNaN(value) ? 0 : Math.min(15, Math.max(0, value))
}

// ---- Evolutions ----

/** The forms the shown one evolves into, step by step (all branches), without loops. */
export function evolutionsAfter(page: PokemonPage, formId: string): string[] {
  const result: string[] = []
  const queue = [formId]
  const seen = new Set(queue)
  while (queue.length > 0) {
    const from = queue.shift()!
    for (const step of page.evolutions) {
      if (step.fromFormId === from && !seen.has(step.toFormId)) {
        seen.add(step.toFormId)
        result.push(step.toFormId)
        queue.push(step.toFormId)
      }
    }
  }
  return result
}

/**
 * The evolution chain as stages for the page: the first stage is every family member nothing evolves into (that
 * takes part in an evolution), each next stage what the previous evolves into, with the step that leads there.
 * A species that doesn't evolve has a single stage with itself.
 */
export interface ChainStage {
  members: { member: FamilyMember; step: PageEvolution | null }[]
}

export function evolutionChain(page: PokemonPage, shownFormId: string): ChainStage[] {
  const byId = new Map(page.family.map((member) => [member.formId, member]))
  const involved = new Set(page.evolutions.flatMap((step) => [step.fromFormId, step.toFormId]))
  const targets = new Set(page.evolutions.map((step) => step.toFormId))
  let current = [...involved].filter((id) => !targets.has(id) && byId.has(id))
  if (current.length === 0) {
    const shown = byId.get(shownFormId)
    return shown ? [{ members: [{ member: shown, step: null }] }] : []
  }
  const stages: ChainStage[] = [
    { members: current.map((id) => ({ member: byId.get(id)!, step: null })) },
  ]
  const seen = new Set(current)
  while (current.length > 0) {
    const next: ChainStage['members'] = []
    for (const from of current) {
      for (const step of page.evolutions) {
        const member = byId.get(step.toFormId)
        if (step.fromFormId === from && member && !seen.has(step.toFormId)) {
          seen.add(step.toFormId)
          next.push({ member, step })
        }
      }
    }
    if (next.length > 0) stages.push({ members: next })
    current = next.map((item) => item.member.formId)
  }
  return stages
}

// ---- Getting it: right now and history ----

/** A local wall-clock time from the API (`2026-11-07T14:00:00`) as a Date in the device's time zone. */
export function wallClock(text: string | null | undefined): Date | null {
  if (!text) return null
  const date = new Date(text.replace(/Z$/, ''))
  return Number.isNaN(date.getTime()) ? null : date
}

/**
 * Right now: started (or no start) and not ended (or open-ended). History: ended. Coming: not started yet (shown
 * with right now, marked as coming).
 */
export function splitAppearances(
  appearances: readonly PageAppearance[],
  now: Date,
): { now: PageAppearance[]; coming: PageAppearance[]; history: PageAppearance[] } {
  const result = {
    now: [] as PageAppearance[],
    coming: [] as PageAppearance[],
    history: [] as PageAppearance[],
  }
  for (const appearance of appearances) {
    const start = wallClock(appearance.startsAt)
    const end = wallClock(appearance.endsAt)
    if (end && end < now) result.history.push(appearance)
    else if (start && start > now) result.coming.push(appearance)
    else result.now.push(appearance)
  }
  const byEnd = (a: PageAppearance, b: PageAppearance) =>
    (wallClock(a.endsAt)?.getTime() ?? Infinity) - (wallClock(b.endsAt)?.getTime() ?? Infinity)
  result.now.sort(byEnd)
  result.coming.sort(
    (a, b) => (wallClock(a.startsAt)?.getTime() ?? 0) - (wallClock(b.startsAt)?.getTime() ?? 0),
  )
  result.history.sort((a, b) => -byEnd(a, b))
  return result
}

// ---- Moves ----

/** Each move's share of its kind's uses (fast moves among fast, charged among charged), biggest first. */
export function usageShares(
  usage: readonly Schemas['MoveUses'][],
  moves: ReadonlyMap<string, PageMove>,
): { fast: { move: PageMove; share: number }[]; charged: { move: PageMove; share: number }[] } {
  const split = (kind: PageMove['kind']) => {
    const rows = usage
      .map((use) => ({ move: moves.get(use.moveId), uses: use.uses }))
      .filter((row): row is { move: PageMove; uses: number } => row.move?.kind === kind)
    const total = rows.reduce((sum, row) => sum + row.uses, 0)
    return rows
      .map((row) => ({ move: row.move, share: total > 0 ? row.uses / total : 0 }))
      .sort((a, b) => b.share - a.share)
  }
  return { fast: split('Fast'), charged: split('Charged') }
}

/** The movepool split into fast and charged moves, each by name. */
export function movepoolOf(
  form: PageForm,
  moves: ReadonlyMap<string, PageMove>,
): { fast: MoveRow[]; charged: MoveRow[] } {
  const rows = form.movepool
    .map((entry) => ({ move: moves.get(entry.moveId), availability: entry.availability }))
    .filter((row): row is MoveRow => !!row.move)
    .sort((a, b) => a.move.nameEn.localeCompare(b.move.nameEn))
  return {
    fast: rows.filter((row) => row.move.kind === 'Fast'),
    charged: rows.filter((row) => row.move.kind === 'Charged'),
  }
}

export interface MoveRow {
  move: PageMove
  availability: Schemas['PageFormMove']['availability']
}

/** The best moveset per PvP league, leagues with the same moveset joined ("Great & Ultra League"). */
export function pvpMovesets(
  form: PageForm,
  shadow: boolean,
): { leagues: League[]; ranking: Schemas['PagePvpRanking'] }[] {
  const groups: { leagues: League[]; ranking: Schemas['PagePvpRanking'] }[] = []
  for (const league of leagues) {
    const ranking = form.pvp.find((r) => r.league === league && r.isShadow === shadow)
    if (!ranking) continue
    const key = [ranking.fastMoveId, ranking.chargedMove1Id, ranking.chargedMove2Id].join('|')
    const same = groups.find(
      (group) =>
        [group.ranking.fastMoveId, group.ranking.chargedMove1Id, group.ranking.chargedMove2Id].join(
          '|',
        ) === key,
    )
    if (same) same.leagues.push(league)
    else groups.push({ leagues: [league], ranking })
  }
  return groups
}

/** The version's types, typed. */
export function typesOf(form: PageForm): PokemonType[] {
  return form.types as PokemonType[]
}

/** The matchups worth showing: weaknesses (biggest first) and resistances (strongest first). */
export function matchupsOf(form: PageForm): {
  weak: Schemas['TypeMultiplier'][]
  resists: Schemas['TypeMultiplier'][]
} {
  return {
    weak: form.matchups
      .filter((m) => m.multiplier > 1.001)
      .sort((a, b) => b.multiplier - a.multiplier),
    resists: form.matchups
      .filter((m) => m.multiplier < 0.999)
      .sort((a, b) => a.multiplier - b.multiplier),
  }
}

/** A multiplier as the game's charts write it: ×2.56, ×1.6, ×0.63, ×0.39, ×0.24. */
export function multiplierText(value: number): string {
  const rounded = Math.round(value * 100) / 100
  return `×${rounded}`
}
