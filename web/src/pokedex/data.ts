import type { Schemas } from '../api/client'
import type { PokemonType } from '../ui/pokemonTypes'
import { compact, normalize } from './normalize'

export type PokedexResponse = Schemas['PokedexResponse']
export type ApiSpecies = Schemas['PokedexSpecies']
export type ApiForm = Schemas['PokedexForm']
export type ApiMove = Schemas['PokedexMove']

/** The region of a regional form, from its id (`vulpix_alolan`, `tauros_paldea_aqua`). */
export type Region = 'alolan' | 'galarian' | 'hisuian' | 'paldean'

/**
 * Where a version sits on its line (decision 2026-11-08): the main form; Megas and Primals; regional forms; other
 * forms with their own stats or types (Origin, Therian…); forms that only look different (no marker).
 */
export type VersionGroup = 'main' | 'mega' | 'regional' | 'other' | 'looks'

/** A species' version, ready for searching and showing. */
export interface Version extends ApiForm {
  group: VersionGroup
  /** For regional forms: which region. */
  region: Region | null
  types: PokemonType[]
  /** Normalized English and Portuguese names, for the search. */
  names: string[]
  fast: ReadonlySet<string>
  charged: ReadonlySet<string>
}

/** One Pokédex line: a species with its versions, the main form first. */
export interface Entry extends Omit<ApiSpecies, 'forms'> {
  versions: Version[]
  main: Version
  /** Normalized species names (English, Portuguese) and nicknames, for the search. */
  names: string[]
  nicknames: string[]
}

export interface Move extends ApiMove {
  type: PokemonType
  /** Normalized names without spaces (English, Portuguese): "firepunch", "socodefogo". */
  keys: string[]
}

/** The whole Pokédex, prepared once per download. */
export interface Pokedex {
  entries: Entry[]
  moves: Move[]
  movesById: ReadonlyMap<string, Move>
}

const regions: Record<string, Region> = {
  alolan: 'alolan',
  galarian: 'galarian',
  hisuian: 'hisuian',
  paldea: 'paldean',
  paldean: 'paldean',
}

/** The region in a regional form's id, or null. */
export function regionOf(id: string): Region | null {
  for (const part of id.split('_')) {
    const region = regions[part]
    if (region) return region
  }
  return null
}

function groupOf(form: ApiForm): VersionGroup {
  if (form.isDefault) return 'main'
  if (form.kind === 'Mega' || form.kind === 'Primal') return 'mega'
  if (form.kind === 'Regional') return 'regional'
  return form.looksOnly ? 'looks' : 'other'
}

function names(...texts: (string | null | undefined)[]): string[] {
  return [...new Set(texts.filter((text): text is string => !!text).map(normalize))]
}

/** Turns the API's answer into what the list and its search work with. */
export function preparePokedex(response: PokedexResponse): Pokedex {
  const moves = response.moves.map((move): Move => ({
    ...move,
    type: move.type as PokemonType,
    keys: [...new Set([move.nameEn, move.namePt].filter((n): n is string => !!n).map(compact))],
  }))
  const entries = response.species.map((species): Entry => {
    const versions = species.forms.map((form): Version => ({
      ...form,
      group: groupOf(form),
      region: form.kind === 'Regional' ? regionOf(form.id) : null,
      types: form.types as PokemonType[],
      names: names(form.nameEn, form.namePt),
      fast: new Set(form.fastMoves),
      charged: new Set(form.chargedMoves),
    }))
    const main = versions.find((version) => version.isDefault) ?? versions[0]
    if (!main) throw new Error(`#${species.dex} has no forms`)
    return {
      dex: species.dex,
      nameEn: species.nameEn,
      namePt: species.namePt,
      generation: species.generation,
      category: species.category,
      familyId: species.familyId,
      versions,
      main,
      names: names(species.nameEn, species.namePt),
      nicknames: names(...species.nicknames),
    }
  })
  return { entries, moves, movesById: new Map(moves.map((move) => [move.id, move])) }
}

/** The region names of generations 1–9, the generation filter's chips and the search's region words. */
export const regionsByGeneration = [
  'kanto',
  'johto',
  'hoenn',
  'sinnoh',
  'unova',
  'kalos',
  'alola',
  'galar',
  'paldea',
] as const

export type RegionName = (typeof regionsByGeneration)[number]
