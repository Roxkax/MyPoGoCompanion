/** The 18 types, in the game's order. Badge colours are the --t-<type> tokens (src/styles/tokens.css). */
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

/** "fire" → "Fire". The Portuguese names come with the languages feature (E01-F04). */
export function typeName(type: PokemonType): string {
  return type.charAt(0).toUpperCase() + type.slice(1)
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

export const versionName: Record<VersionKind, string> = {
  mega: 'Mega',
  primal: 'Primal',
  shadow: 'Shadow',
  dynamax: 'Dynamax',
  gmax: 'G-Max',
  alolan: 'Alolan',
  galarian: 'Galarian',
  hisuian: 'Hisuian',
  paldean: 'Paldean',
}
