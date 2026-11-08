import { en } from '../i18n/messages/en'
import { ptPT } from '../i18n/messages/pt-PT'
import { pokemonTypes, type PokemonType } from '../ui/pokemonTypes'
import {
  regionsByGeneration,
  type Entry,
  type Pokedex,
  type Region,
  type RegionName,
  type Version,
} from './data'
import { compact, normalize } from './normalize'

/*
 * The Pokédex search (decision 2026-11-08): everything is searchable, with the game's own rules. A comma (or ; and :)
 * means either/or, & means both, ! means not; a space also means both, but a phrase is tried as a whole name first
 * ("mega charizard y", "@fire punch"). Words of the same group are alternatives: "fire,water,rock". Every version
 * of a Pokémon counts: a line matches when one of its versions matches everything.
 */

export type Stat = 'attack' | 'defense' | 'stamina' | 'maxCp'
export type Category = 'Legendary' | 'Mythical' | 'UltraBeast'
export type VersionFlag = 'mega' | 'primal' | 'shadow' | 'dynamax' | 'gmax'

/** One word or phrase, as the search understood it. */
export type Term =
  | { kind: 'name'; text: string; species: ReadonlySet<number>; forms: ReadonlySet<string> }
  | { kind: 'number'; from: number; to: number }
  | { kind: 'type'; type: PokemonType }
  | { kind: 'generation'; generation: number }
  | { kind: 'hisui' }
  | { kind: 'regional'; region: Region }
  | { kind: 'version'; version: VersionFlag }
  | { kind: 'category'; category: Category }
  | { kind: 'released'; released: boolean }
  | { kind: 'move'; text: string; ids: ReadonlySet<string> }
  | {
      kind: 'moveType'
      type: PokemonType
      slot: 'any' | 'fast' | 'charged'
      ids: ReadonlySet<string>
    }
  | { kind: 'family'; text: string; families: ReadonlySet<string> }
  | { kind: 'stat'; stat: Stat; min: number | null; max: number | null }
  | { kind: 'unknown'; text: string }

/** An alternative of a group: a term, maybe with "not". */
export interface Alternative {
  term: Term
  negated: boolean
  /** What was typed for it. */
  text: string
}

/** Alternatives joined by commas: one of them must match. */
export interface Group {
  alternatives: Alternative[]
}

/** Groups joined by & or spaces: every group must match (on the same version). */
export interface Query {
  groups: Group[]
}

export const emptyQuery: Query = { groups: [] }

/** Pokédex numbers of the Hisui Pokémon (Wyrdeer to Enamorus): the game's "hisui" region. */
export const hisuiDex = { from: 899, to: 905 } as const

const statWords: Record<string, Stat> = {
  atk: 'attack',
  attack: 'attack',
  ataque: 'attack',
  def: 'defense',
  defense: 'defense',
  defence: 'defense',
  defesa: 'defense',
  sta: 'stamina',
  stamina: 'stamina',
  hp: 'stamina',
  ps: 'stamina',
  resistencia: 'stamina',
  cp: 'maxCp',
  pc: 'maxCp',
}

/** Every keyword in both languages, normalized, with the term it stands for. */
const keywords: ReadonlyMap<string, Term> = (() => {
  const map = new Map<string, Term>()
  const add = (term: Term, ...words: string[]) =>
    words.forEach((word) => map.set(normalize(word), term))
  for (const type of pokemonTypes)
    add({ kind: 'type', type }, type, en.types[type], ptPT.types[type])
  regionsByGeneration.forEach((region: RegionName, index) =>
    add(
      { kind: 'generation', generation: index + 1 },
      region,
      `gen${index + 1}`,
      `gen ${index + 1}`,
    ),
  )
  add({ kind: 'hisui' }, 'hisui')
  add({ kind: 'regional', region: 'alolan' }, 'alolan', 'alolano', 'alolana', 'de alola')
  add({ kind: 'regional', region: 'galarian' }, 'galarian', 'galariano', 'galariana', 'de galar')
  add({ kind: 'regional', region: 'hisuian' }, 'hisuian', 'hisuiano', 'hisuiana', 'de hisui')
  add({ kind: 'regional', region: 'paldean' }, 'paldean', 'paldeano', 'paldeana', 'de paldea')
  add({ kind: 'version', version: 'mega' }, 'mega', 'megas')
  add({ kind: 'version', version: 'primal' }, 'primal', 'primitivo', 'primitiva')
  add({ kind: 'version', version: 'shadow' }, 'shadow', 'sombroso', 'sombrosa')
  add({ kind: 'version', version: 'dynamax' }, 'dynamax', 'dinamax')
  add({ kind: 'version', version: 'gmax' }, 'gmax', 'g max', 'gigantamax')
  add(
    { kind: 'category', category: 'Legendary' },
    'legendary',
    'legendaries',
    'lendario',
    'lendarios',
  )
  add(
    { kind: 'category', category: 'Mythical' },
    'mythical',
    'mythic',
    'mitico',
    'miticos',
    'singular',
  )
  add(
    { kind: 'category', category: 'UltraBeast' },
    'ultrabeast',
    'ultrabeasts',
    'ultra beast',
    'ultra beasts',
    'ultracriatura',
    'ultracriaturas',
  )
  add({ kind: 'released', released: true }, 'released', 'lancado', 'lancados', 'disponivel')
  add(
    { kind: 'released', released: false },
    'unreleased',
    'not released',
    'nao lancado',
    'nao lancados',
    'indisponivel',
  )
  return map
})()

/** Type names in both languages (for @fire / @fogo). */
const typeWords: ReadonlyMap<string, PokemonType> = new Map(
  pokemonTypes.flatMap((type) =>
    [type, en.types[type], ptPT.types[type]].map((word) => [normalize(word), type] as const),
  ),
)

/** Whether a name (normalized) answers to what was typed: from its start, from a word's start, or inside it (3+ letters). */
function nameAnswers(name: string, typed: string): boolean {
  return (
    name.startsWith(typed) ||
    name.split(' ').some((word) => word.startsWith(typed)) ||
    (typed.length >= 3 && name.includes(typed))
  )
}

interface NameMatch {
  species: Set<number>
  forms: Set<string>
}

/** Species whose name or nickname answers to the text; else the versions whose own name does ("mega charizard y"). */
function matchNames(pokedex: Pokedex, text: string, exactOnly = false): NameMatch {
  const typed = normalize(text)
  const result: NameMatch = { species: new Set(), forms: new Set() }
  if (!typed) return result
  const test = (name: string) =>
    exactOnly ? name === typed || name.startsWith(typed) : nameAnswers(name, typed)
  for (const entry of pokedex.entries) {
    if (
      entry.names.some(test) ||
      entry.nicknames.some((nick) => nick === typed || nick.startsWith(typed))
    ) {
      result.species.add(entry.dex)
    } else {
      for (const version of entry.versions) {
        if (version.names.some(test)) result.forms.add(version.id)
      }
    }
  }
  return result
}

/** Moves whose name is the text (spaces don't matter); else those starting with it; else those containing it. */
function matchMoves(pokedex: Pokedex, text: string): Set<string> {
  const typed = compact(text)
  if (!typed) return new Set()
  const pick = (test: (key: string) => boolean) =>
    new Set(pokedex.moves.filter((move) => move.keys.some(test)).map((move) => move.id))
  const exact = pick((key) => key === typed)
  if (exact.size) return exact
  const starting = pick((key) => key.startsWith(typed))
  if (starting.size || typed.length < 3) return starting
  return pick((key) => key.includes(typed))
}

const numberPattern = /^#?(\d+)?(-)?(\d+)?$/
const statPattern = /^([a-z]+)(\d+)?(-)?(\d+)?$/

function range(min: string | undefined, dash: string | undefined, max: string | undefined) {
  const low = min ? Number(min) : null
  const high = max ? Number(max) : null
  if (low === null && high === null) return null
  // "250" is exactly 250; "250-" is 250 or more; "-150" is 150 or less; "200-300" is between.
  return dash ? { min: low, max: high } : { min: low, max: low }
}

/** Reads one word or phrase (without its "!"); null when it isn't a whole recognised phrase and `strict` is set. */
function read(pokedex: Pokedex, raw: string, strict: boolean): Term | null {
  const text = raw.trim()
  const word = normalize(text)

  if (!strict) {
    const number = numberPattern.exec(text)
    if (number && (number[1] || number[3])) {
      const from = number[1] ? Number(number[1]) : 1
      const to = number[3] ? Number(number[3]) : number[2] ? Number.MAX_SAFE_INTEGER : from
      return { kind: 'number', from, to }
    }
    const stat = statPattern.exec(text.toLowerCase())
    const which = stat ? statWords[stat[1] ?? ''] : undefined
    if (stat && which) {
      const bounds = range(stat[2], stat[3], stat[4])
      if (bounds) return { kind: 'stat', stat: which, ...bounds }
    }
  }

  if (text.startsWith('@')) {
    const rest = text.slice(1)
    const slotted = /^([123])(.*)$/.exec(rest)
    const type = typeWords.get(normalize(slotted ? (slotted[2] ?? '') : rest))
    if (type) {
      const slot = slotted ? (slotted[1] === '1' ? 'fast' : 'charged') : 'any'
      const ids = new Set(
        pokedex.moves
          .filter(
            (move) =>
              move.type === type &&
              (slot === 'any' || (slot === 'fast') === (move.kind === 'Fast')),
          )
          .map((move) => move.id),
      )
      return { kind: 'moveType', type, slot, ids }
    }
    const ids = matchMoves(pokedex, rest)
    if (ids.size) return { kind: 'move', text: rest.trim(), ids }
    return strict ? null : { kind: 'unknown', text }
  }

  if (text.startsWith('+')) {
    const rest = text.slice(1)
    const names = matchNames(pokedex, rest, true)
    const families = new Set(
      pokedex.entries
        .filter((entry) => names.species.has(entry.dex))
        .map((entry) => entry.familyId),
    )
    if (families.size) return { kind: 'family', text: rest.trim(), families }
    return strict ? null : { kind: 'unknown', text }
  }

  const keyword = keywords.get(word)
  if (keyword) return keyword

  const names = matchNames(pokedex, text, strict)
  if (names.species.size || names.forms.size) return { kind: 'name', text, ...names }
  return strict ? null : { kind: 'unknown', text }
}

interface Word {
  text: string
  /** What comes after it: a space, an "or" (, ; :), an "and" (&), or the end. */
  next: 'space' | 'or' | 'and' | 'end'
}

/** Splits what was typed into words and the separators between them. Spaces around , and & don't count. */
export function words(input: string): Word[] {
  const result: Word[] = []
  let current = ''
  // The strongest separator since the last word: "or" and "and" beat spaces.
  let gap: Word['next'] | null = null
  for (const char of input) {
    const separator =
      char === ',' || char === ';' || char === ':'
        ? 'or'
        : char === '&'
          ? 'and'
          : /\s/.test(char)
            ? 'space'
            : null
    if (separator === null) {
      const previous = result.at(-1)
      if (!current && gap && previous) previous.next = gap
      gap = null
      current += char
      continue
    }
    if (current) {
      result.push({ text: current, next: 'end' })
      current = ''
    }
    if (gap === null || gap === 'space' || separator !== 'space') gap = separator
  }
  if (current) result.push({ text: current, next: 'end' })
  return result
}

/** Words that are only a symbol so far ("!", "@", "+"): skipped while typing. */
const bare = new Set(['', '@', '+'])

/** Reads what was typed in the search box. */
export function parseQuery(input: string, pokedex: Pokedex): Query {
  const list = words(input)
  const groups: Group[] = []
  let alternatives: Alternative[] = []
  const close = (word: Word) => {
    if (word.next !== 'or' && alternatives.length) {
      groups.push({ alternatives })
      alternatives = []
    }
  }
  let index = 0
  for (let first = list[index]; first; first = list[index]) {
    const negated = first.text.startsWith('!')
    const start = negated ? first.text.slice(1) : first.text
    if (bare.has(start)) {
      close(first)
      index++
      continue
    }

    // The longest phrase first: up to 5 words joined by spaces that read as one name, move or keyword.
    let last = index
    while (list[last]?.next === 'space' && last < list.length - 1 && last - index < 4) last++
    let end = index
    let term: Term | null = null
    for (let candidate = last; candidate > index && !term; candidate--) {
      const rest = list.slice(index + 1, candidate + 1).map((word) => word.text)
      term = read(pokedex, [start, ...rest].join(' '), true)
      if (term) end = candidate
    }
    term ??= read(pokedex, start, false)
    const text = list
      .slice(index, end + 1)
      .map((word) => word.text)
      .join(' ')
    alternatives.push({ term: term ?? { kind: 'unknown', text: start }, negated, text })
    close(list[end] ?? first)
    index = end + 1
  }
  if (alternatives.length) groups.push({ alternatives })
  return { groups }
}

/** Whether a term is about the species as a whole: "not" then means no version at all. */
function aboutSpecies(term: Term): boolean {
  return !['type', 'stat', 'move', 'moveType', 'released', 'unknown'].includes(term.kind)
}

/** Whether this version of this Pokémon has what the term asks for. */
export function termMatches(term: Term, entry: Entry, version: Version): boolean {
  switch (term.kind) {
    case 'name':
      return term.species.has(entry.dex) || term.forms.has(version.id)
    case 'number':
      return entry.dex >= term.from && entry.dex <= term.to
    case 'type':
      return version.types.includes(term.type)
    case 'generation':
      return entry.generation === term.generation
    case 'hisui':
      return entry.dex >= hisuiDex.from && entry.dex <= hisuiDex.to
    case 'regional':
      return version.region === term.region
    case 'version':
      switch (term.version) {
        case 'mega':
          return version.kind === 'Mega'
        case 'primal':
          return version.kind === 'Primal'
        case 'shadow':
          return version.shadow
        case 'dynamax':
          return version.dynamax
        case 'gmax':
          return version.gigantamax
      }
      return false
    case 'category':
      return entry.category === term.category
    case 'released':
      return version.released === term.released
    case 'move':
      return [...term.ids].some((id) => version.fast.has(id) || version.charged.has(id))
    case 'moveType':
      return [...term.ids].some((id) => version.fast.has(id) || version.charged.has(id))
    case 'family':
      return term.families.has(entry.familyId)
    case 'stat': {
      const value = version[term.stat]
      return (term.min === null || value >= term.min) && (term.max === null || value <= term.max)
    }
    case 'unknown':
      return false
  }
}

/** Whether this version, among the Pokémon's versions in play, matches every group of the query. */
export function queryMatches(
  query: Query,
  entry: Entry,
  version: Version,
  versions: readonly Version[],
): boolean {
  return query.groups.every((group) =>
    group.alternatives.some(({ term, negated }) => {
      if (!negated) return termMatches(term, entry, version)
      if (aboutSpecies(term)) return !versions.some((other) => termMatches(term, entry, other))
      return !termMatches(term, entry, version)
    }),
  )
}

/** The stats the query asks about (not negated), in the order that picks the version a line shows. */
export function queryStats(query: Query): Stat[] {
  return query.groups.flatMap((group) =>
    group.alternatives.flatMap(({ term, negated }) =>
      term.kind === 'stat' && !negated ? [term.stat] : [],
    ),
  )
}
