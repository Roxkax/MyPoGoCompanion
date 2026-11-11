import type { Schemas } from '../api/client'
import { locales, type Language } from '../i18n/languages'
import { compact } from '../pokedex/normalize'
import { isPokemonType, type PokemonType } from '../ui/pokemonTypes'

export type Move = Schemas['PageMove']
export const sortKeys = [
  'name',
  'type',
  'raidPower',
  'raidDurationMs',
  'raidEnergy',
  'pvpPower',
  'pvpEnergy',
  'pvpTurns',
] as const
export type MoveSort = (typeof sortKeys)[number]
export type SortOrder = 'asc' | 'desc'

export interface MoveFilters {
  q: string
  kind: 'fast' | 'charged'
  type: PokemonType | null
  mode: 'raids' | 'pvp'
  sort: MoveSort
  order: SortOrder
}

export const noFilters: MoveFilters = {
  q: '',
  kind: 'charged',
  type: null,
  mode: 'raids',
  sort: 'raidPower',
  order: 'desc',
}

/** Names and shorter timings start from the smallest value; power and energy start from the largest. */
export function defaultOrder(sort: MoveSort): SortOrder {
  return sort === 'name' || sort === 'type' || sort === 'raidDurationMs' || sort === 'pvpTurns'
    ? 'asc'
    : 'desc'
}

/** Unknown URL values are ignored; the same address can be opened in either layout. */
export function readFilters(params: URLSearchParams): MoveFilters {
  const rawType = (params.get('type') ?? '').toLowerCase()
  const sort = sortKeys.find((key) => key === params.get('sort')) ?? noFilters.sort
  const rawOrder = params.get('order')
  return {
    q: params.get('q') ?? '',
    kind: params.get('kind') === 'fast' ? 'fast' : 'charged',
    type: isPokemonType(rawType) ? rawType : null,
    mode: params.get('mode') === 'pvp' ? 'pvp' : 'raids',
    sort,
    order: rawOrder === 'asc' || rawOrder === 'desc' ? rawOrder : defaultOrder(sort),
  }
}

/** Keep only choices that differ from the initial view. */
export function writeFilters(filters: MoveFilters): URLSearchParams {
  const params = new URLSearchParams()
  if (filters.q) params.set('q', filters.q)
  if (filters.kind !== noFilters.kind) params.set('kind', filters.kind)
  if (filters.type) params.set('type', filters.type)
  if (filters.mode !== noFilters.mode) params.set('mode', filters.mode)
  if (filters.sort !== noFilters.sort) params.set('sort', filters.sort)
  if (filters.order !== defaultOrder(filters.sort)) params.set('order', filters.order)
  return params
}

export function sortBy(filters: MoveFilters, sort: MoveSort): MoveFilters {
  return {
    ...filters,
    sort,
    order: filters.sort === sort ? (filters.order === 'asc' ? 'desc' : 'asc') : defaultOrder(sort),
  }
}

/** The name in the chosen language, with the game's English name as the fallback. */
export function moveName(move: Move, language: Language): string {
  return (language === 'pt-PT' ? move.namePt : null) ?? move.nameEn
}

/** Filter and sort a copy so switching the view never changes the cached response. */
export function filterMoves(
  moves: readonly Move[],
  filters: MoveFilters,
  language: Language,
  typeName: (type: PokemonType) => string = (type) => type,
): Move[] {
  const query = compact(filters.q)
  const collator = new Intl.Collator(locales[language], { sensitivity: 'base', numeric: true })
  const name = (move: Move) => moveName(move, language)
  const compareNames = (a: Move, b: Move) =>
    collator.compare(name(a), name(b)) || a.id.localeCompare(b.id)
  const value = (move: Move): string | number | null => {
    if (filters.sort === 'name') return name(move)
    if (filters.sort === 'type') {
      return isPokemonType(move.type) ? typeName(move.type) : move.type
    }
    if (filters.sort.startsWith('raid') && move.raidDurationMs <= 0) return null
    return move[filters.sort]
  }
  return moves
    .filter(
      (move) =>
        move.kind === (filters.kind === 'fast' ? 'Fast' : 'Charged') &&
        (filters.type === null || move.type === filters.type) &&
        (!query ||
          [move.nameEn, move.namePt, move.id].some(
            (text) => text && compact(text).includes(query),
          )),
    )
    .sort((a, b) => {
      const left = value(a)
      const right = value(b)
      // Missing timings stay after known ones in either direction.
      if (left === null || right === null) {
        if (left === null && right === null) return compareNames(a, b)
        return left === null ? 1 : -1
      }
      const compared =
        typeof left === 'string' && typeof right === 'string'
          ? collator.compare(left, right)
          : Number(left) - Number(right)
      return compared * (filters.order === 'asc' ? 1 : -1) || compareNames(a, b)
    })
}

const toPvp: Partial<Record<MoveSort, MoveSort>> = {
  raidPower: 'pvpPower',
  raidDurationMs: 'pvpTurns',
  raidEnergy: 'pvpEnergy',
}
const toRaids: Partial<Record<MoveSort, MoveSort>> = {
  pvpPower: 'raidPower',
  pvpTurns: 'raidDurationMs',
  pvpEnergy: 'raidEnergy',
}

/** Keep the corresponding visible stat selected when changing the phone's numbers. */
export function setMode(filters: MoveFilters, mode: MoveFilters['mode']): MoveFilters {
  const converted = (mode === 'pvp' ? toPvp : toRaids)[filters.sort] ?? filters.sort
  const sort = converted === 'pvpTurns' && filters.kind === 'charged' ? 'pvpPower' : converted
  return {
    ...filters,
    mode,
    sort,
    order: sort !== converted ? defaultOrder(sort) : filters.order,
  }
}

/** Charged moves have no turn count, so that ordering becomes PvP power. */
export function setKind(filters: MoveFilters, kind: MoveFilters['kind']): MoveFilters {
  if (kind === 'charged' && filters.sort === 'pvpTurns') {
    return { ...filters, kind, sort: 'pvpPower', order: defaultOrder('pvpPower') }
  }
  return { ...filters, kind }
}
