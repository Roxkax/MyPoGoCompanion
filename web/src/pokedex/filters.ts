import { isPokemonType, type PokemonType } from '../ui/pokemonTypes'
import type { Category, Stat } from './search'

/*
 * The Pokédex list's filters and sort, kept in the address (decision 2026-11-08) so Back, reloading and sharing a link
 * keep the view: /pokedex?q=fire,water&type=fire&gen=1,4&atk=230-&has=mega&cat=legendary&released=all&sort=atk.
 * Anything the address gets wrong is ignored (the screen checks its own values).
 */

export type VersionFilter = 'mega' | 'shadow' | 'dynamax' | 'gmax' | 'regional'
export type SortKey = 'number' | 'name' | Stat
export type SortOrder = 'asc' | 'desc'

export interface StatRange {
  min: number | null
  max: number | null
}

export interface Filters {
  /** What was typed in the search box. */
  q: string
  /** One or two types: a version must have all of them. */
  types: PokemonType[]
  /** Generations 1–9: any of them. */
  generations: number[]
  ranges: Partial<Record<Stat, StatRange>>
  /** Versions a Pokémon must have (each one is needed). */
  versions: VersionFilter[]
  /** Legendary, Mythical, Ultra Beast: any of them. */
  categories: Category[]
  /** Only versions released in GO (on unless the address says released=all). */
  releasedOnly: boolean
  sort: SortKey
  order: SortOrder
}

export const stats: readonly Stat[] = ['attack', 'defense', 'stamina', 'maxCp']
export const versionFilters: readonly VersionFilter[] = [
  'mega',
  'shadow',
  'dynamax',
  'gmax',
  'regional',
]
export const categories: readonly Category[] = ['Legendary', 'Mythical', 'UltraBeast']
export const sortKeys: readonly SortKey[] = ['number', 'name', ...stats]

/** The address's names for the stats. */
const statParams: Record<Stat, string> = {
  attack: 'atk',
  defense: 'def',
  stamina: 'sta',
  maxCp: 'cp',
}
const sortParams: Record<SortKey, string> = { number: 'num', name: 'name', ...statParams }
const categoryParams: Record<Category, string> = {
  Legendary: 'legendary',
  Mythical: 'mythical',
  UltraBeast: 'ultrabeast',
}

/** The first tap on a column: numbers and names from the start, stats from the best. */
export function defaultOrder(sort: SortKey): SortOrder {
  return sort === 'number' || sort === 'name' ? 'asc' : 'desc'
}

export const noFilters: Filters = {
  q: '',
  types: [],
  generations: [],
  ranges: {},
  versions: [],
  categories: [],
  releasedOnly: true,
  sort: 'number',
  order: 'asc',
}

function list(params: URLSearchParams, name: string): string[] {
  return (params.get(name) ?? '')
    .split(',')
    .map((value) => value.trim().toLowerCase())
    .filter(Boolean)
}

function parseRange(text: string | null): StatRange | null {
  const match = /^(\d+)?-(\d+)?$/.exec(text ?? '') ?? /^(\d+)$/.exec(text ?? '')
  if (!match) return null
  const min = match[1] ? Number(match[1]) : null
  const max = match.length > 2 ? (match[2] ? Number(match[2]) : null) : min
  return min === null && max === null ? null : { min, max }
}

function formatRange({ min, max }: StatRange): string {
  return min !== null && min === max ? String(min) : `${min ?? ''}-${max ?? ''}`
}

/** The filters in an address's query. */
export function readFilters(params: URLSearchParams): Filters {
  const types = [...new Set(list(params, 'type').filter(isPokemonType))].slice(0, 2)
  const generations = [...new Set(list(params, 'gen').map(Number))]
    .filter((gen) => Number.isInteger(gen) && gen >= 1 && gen <= 9)
    .sort((a, b) => a - b)
  const ranges: Filters['ranges'] = {}
  for (const stat of stats) {
    const range = parseRange(params.get(statParams[stat]))
    if (range) ranges[stat] = range
  }
  const has = list(params, 'has')
  const cats = list(params, 'cat')
  const sortParam = params.get('sort')
  const sort = sortKeys.find((key) => sortParams[key] === sortParam) ?? 'number'
  const orderParam = params.get('order')
  return {
    q: params.get('q') ?? '',
    types,
    generations,
    ranges,
    versions: versionFilters.filter((filter) => has.includes(filter)),
    categories: categories.filter((category) => cats.includes(categoryParams[category])),
    releasedOnly: params.get('released') !== 'all',
    sort,
    order: orderParam === 'asc' || orderParam === 'desc' ? orderParam : defaultOrder(sort),
  }
}

/** The address's query for these filters: only what differs from no filters. */
export function writeFilters(filters: Filters): URLSearchParams {
  const params = new URLSearchParams()
  if (filters.q) params.set('q', filters.q)
  if (filters.types.length) params.set('type', filters.types.join(','))
  if (filters.generations.length) params.set('gen', filters.generations.join(','))
  for (const stat of stats) {
    const range = filters.ranges[stat]
    if (range && (range.min !== null || range.max !== null))
      params.set(statParams[stat], formatRange(range))
  }
  if (filters.versions.length) params.set('has', filters.versions.join(','))
  if (filters.categories.length) {
    params.set('cat', filters.categories.map((category) => categoryParams[category]).join(','))
  }
  if (!filters.releasedOnly) params.set('released', 'all')
  if (filters.sort !== 'number') params.set('sort', sortParams[filters.sort])
  if (filters.order !== defaultOrder(filters.sort)) params.set('order', filters.order)
  return params
}

/** How many filters are set (the search box and the sort don't count; "Released" counts when it's off). */
export function countFilters(filters: Filters): number {
  return (
    filters.types.length +
    filters.generations.length +
    Object.values(filters.ranges).filter(Boolean).length +
    filters.versions.length +
    filters.categories.length
  )
}

/** Types toggle; a third type replaces the first (a Pokémon has at most two). */
export function toggleType(types: readonly PokemonType[], type: PokemonType): PokemonType[] {
  if (types.includes(type)) return types.filter((other) => other !== type)
  return [...types, type].slice(-2)
}

/** Tapping a column: the same column reverses, another starts in its own direction. */
export function sortBy(filters: Filters, sort: SortKey): Filters {
  const order: SortOrder =
    filters.sort === sort ? (filters.order === 'asc' ? 'desc' : 'asc') : defaultOrder(sort)
  return { ...filters, sort, order }
}
