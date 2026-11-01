/**
 * The 18 types, in the game's order. Badge colours are the --t-<type> tokens (src/styles/tokens.css); names are the
 * texts types.<type>, versions' names versions.<kind>.
 */
export const pokemonTypes = [
  'normal',
  'fire',
  'water',
  'grass',
  'electric',
  'ice',
  'fighting',
  'poison',
  'ground',
  'flying',
  'psychic',
  'bug',
  'rock',
  'ghost',
  'dragon',
  'dark',
  'steel',
  'fairy',
] as const

export type PokemonType = (typeof pokemonTypes)[number]

export function isPokemonType(value: string): value is PokemonType {
  return (pokemonTypes as readonly string[]).includes(value)
}

/** The versions a Pokémon can have, shown as markers on its line (B1) and as the version switcher (B2). */
export const versionKinds = [
  'mega',
  'primal',
  'shadow',
  'dynamax',
  'gmax',
  'alolan',
  'galarian',
  'hisuian',
  'paldean',
] as const

export type VersionKind = (typeof versionKinds)[number]
