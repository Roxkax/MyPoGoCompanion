import { isPokemonType } from '../../ui/pokemonTypes'

export const groups = [
  'pokemon',
  'types',
  'appraisal',
  'power',
  'special',
  'rarity',
  'caught',
  'moves',
  'actions',
  'tags',
] as const
export type Group = (typeof groups)[number]
export type Condition = { group: Group | 'custom'; terms: string[] }

export const keywords = {
  pokemon: [
    'kanto',
    'johto',
    'hoenn',
    'sinnoh',
    'unova',
    'kalos',
    'alola',
    'galar',
    'hisui',
    'paldea',
  ],
  types: [],
  appraisal: ['0*', '1*', '2*', '3*', '4*'],
  power: [],
  special: ['shiny', 'lucky', 'shadow', 'purified', 'costume', 'background', 'locationbackground'],
  rarity: ['legendary', 'mythical', 'ultrabeast', 'eggsonly'],
  caught: ['age0', 'traded', 'hatched'],
  moves: ['@special', '@weather'],
  actions: [
    'evolve',
    'evolvenew',
    'evolvequest',
    'tradeevolve',
    'megaevolve',
    'item',
    'dynamax',
    'gigantamax',
    'fusion',
  ],
  tags: ['favorite', 'defender', 'hypertraining', 'xxs', 'xs', 'xl', 'xxl'],
} as const satisfies Record<Group, readonly string[]>

type Range = { prefix: string; suffix?: string; min: number; max?: number }
export const ranges = {
  dex: { prefix: '', min: 1 },
  attack: { prefix: '', suffix: 'attack', min: 0, max: 4 },
  defense: { prefix: '', suffix: 'defense', min: 0, max: 4 },
  hpBar: { prefix: '', suffix: 'hp', min: 0, max: 4 },
  cp: { prefix: 'cp', min: 0 },
  hp: { prefix: 'hp', min: 0 },
  age: { prefix: 'age', min: 0 },
  year: { prefix: 'year', min: 2016 },
  distance: { prefix: 'distance ', min: 0 },
  buddy: { prefix: 'buddy', min: 0, max: 5 },
  mega: { prefix: 'mega', min: 0, max: 3 },
  maxmove: { prefix: 'maxmove', min: 0, max: 3 },
} satisfies Record<string, Range>
export type RangeKind = keyof typeof ranges
export const groupRanges: Record<Group, readonly RangeKind[]> = {
  pokemon: ['dex'],
  types: [],
  appraisal: ['attack', 'defense', 'hpBar'],
  power: ['cp', 'hp'],
  special: [],
  rarity: [],
  caught: ['age', 'year', 'distance'],
  moves: [],
  actions: ['buddy', 'mega', 'maxmove'],
  tags: [],
}

export const characterLimit = 1950
export const inputLimit = 19500

export function rangeTerm(kind: RangeKind, low: string, high: string): string | null {
  const { prefix, suffix, min, max = 2_147_483_647 }: Range = ranges[kind]
  const isBound = (value: string) => /^[0-9]+$/.test(value) && +value >= min && +value <= max
  if ((!low && !high) || (low && !isBound(low)) || (high && !isBound(high))) return null
  if (low && high && +low > +high) return null
  const first = low ? String(Number(low)) : ''
  const last = high ? String(Number(high)) : ''
  const value = first === last ? first : `${first}-${last}`
  return `${prefix}${value}${suffix ?? ''}`
}

const rangeKinds = Object.keys(ranges) as RangeKind[]

export function readRange(body: string) {
  const match =
    /^(cp|hp|age|year|distance|buddy|mega|maxmove)? ?(\d+)?(?:(-)(\d+)?)?(attack|defense|hp)?$/.exec(
      body,
    )
  if (!match || (!match[2] && !match[4])) return null
  const [, prefix, first, dash, last, suffix] = match
  const kind = rangeKinds.find((key) => {
    const range: Range = ranges[key]
    return range.prefix.trim() === (prefix ?? '') && range.suffix === suffix
  })
  if (!kind) return null
  const low = first ? Number(first) : null
  const high = dash ? (last ? Number(last) : null) : low
  if (
    (low !== null && !Number.isSafeInteger(low)) ||
    (high !== null && !Number.isSafeInteger(high))
  )
    return null
  return { kind, low, high }
}

function groupFor(term: string, names: ReadonlySet<string>): Condition['group'] {
  const body = term.replace(/^!/, '').toLowerCase()
  const keywordGroup = groups.find((group) => (keywords[group] as readonly string[]).includes(body))
  if (keywordGroup) return keywordGroup
  if (isPokemonType(body)) return 'types'
  if (body.startsWith('@')) return 'moves'
  if (body.startsWith('#')) return 'tags'
  if (body.startsWith('+') || names.has(body)) return 'pokemon'
  const range = readRange(body)
  if (range) return groups.find((group) => groupRanges[group].includes(range.kind)) ?? 'custom'
  if (/^[\d-]+\*$/.test(body)) return 'appraisal'
  return 'custom'
}

/** The separators have the same precedence as the in-game search. Keep every alternative, even custom ones. */
export function readConditions(expression: string, names: ReadonlySet<string>): Condition[] | null {
  if (expression.length > inputLimit) return null
  if (!expression.trim()) return []
  const conditions: Condition[] = []
  for (const clause of expression.split(/[&|]/)) {
    const terms = clause.split(/[,;:]/).map((term) => term.trim())
    if (terms.some((term) => !term || term === '!')) return null
    const group = groupFor(terms[0] ?? '', names)
    conditions.push({
      group: terms.every((term) => groupFor(term, names) === group) ? group : 'custom',
      terms,
    })
  }
  return conditions
}

export function formatConditions(conditions: readonly Condition[]): string {
  return conditions.map(({ terms }) => terms.join(',')).join('&')
}

export function customTerm(value: string, prefix = ''): string | null {
  const term = (prefix === '#' ? value.trim().replace(/^#/, '') : value).trim()
  return term && !/[&|,;:!()\p{Cc}]/u.test(term) ? prefix + term : null
}
