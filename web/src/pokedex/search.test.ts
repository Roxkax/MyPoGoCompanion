import { describe, expect, it } from 'vitest'
import { samplePokedex } from '../testing/pokedex'
import { preparePokedex, regionOf } from './data'
import { noFilters } from './filters'
import { compact, normalize } from './normalize'
import { parseQuery, queryStats, words, type Term } from './search'
import { buildRows } from './view'

const pokedex = preparePokedex(samplePokedex)

/** The Pokédex numbers a search finds (released versions only, as the list starts). */
function find(query: string, releasedOnly = true): number[] {
  return buildRows(pokedex, { ...noFilters, releasedOnly }, parseQuery(query, pokedex)).map(
    (row) => row.entry.dex,
  )
}

/** The version each line shows, by Pokédex number. */
function shown(query: string): Record<number, string> {
  return Object.fromEntries(
    buildRows(pokedex, noFilters, parseQuery(query, pokedex)).map((row) => [
      row.entry.dex,
      row.shown.id,
    ]),
  )
}

function terms(query: string): Term['kind'][][] {
  return parseQuery(query, pokedex).groups.map((group) =>
    group.alternatives.map((alt) => alt.term.kind),
  )
}

describe('normalize', () => {
  it('drops accents, capitals, apostrophes and dots', () => {
    expect(normalize("Farfetch'd")).toBe('farfetchd')
    expect(normalize('Mr. Mime')).toBe('mr mime')
    expect(normalize('  Elétrico—Água ')).toBe('eletrico agua')
    expect(compact('Fire Punch')).toBe('firepunch')
  })
})

describe('preparePokedex', () => {
  it('refuses a species without forms', () => {
    const [first] = samplePokedex.species
    expect(() => preparePokedex({ moves: [], species: [{ ...first!, forms: [] }] })).toThrow(
      '#6 has no forms',
    )
  })
})

describe('regionOf', () => {
  it('reads the region from a regional form id', () => {
    expect(regionOf('vulpix_alolan')).toBe('alolan')
    expect(regionOf('tauros_paldea_aqua')).toBe('paldean')
    expect(regionOf('darmanitan_galarian_zen')).toBe('galarian')
    expect(regionOf('giratina_origin')).toBeNull()
  })
})

describe('words', () => {
  it('splits words and keeps the separator after each', () => {
    expect(words('fire , water&  rock  kanto,')).toEqual([
      { text: 'fire', next: 'or' },
      { text: 'water', next: 'and' },
      { text: 'rock', next: 'space' },
      { text: 'kanto', next: 'end' },
    ])
    expect(words('  ')).toEqual([])
    expect(words('a;b:c')).toEqual([
      { text: 'a', next: 'or' },
      { text: 'b', next: 'or' },
      { text: 'c', next: 'end' },
    ])
  })
})

describe('parseQuery', () => {
  it('reads commas as either/or and spaces or & as both', () => {
    expect(terms('fire,water,rock')).toEqual([['type', 'type', 'type']])
    expect(terms('fire&flying')).toEqual([['type'], ['type']])
    expect(terms('fire kanto')).toEqual([['type'], ['generation']])
    expect(terms('fire,water kanto')).toEqual([['type', 'type'], ['generation']])
  })

  it('reads a phrase as one name or move first', () => {
    expect(terms('mega charizard y')).toEqual([['name']])
    expect(terms('mr mime')).toEqual([['name']])
    expect(terms('@fire punch,@ice beam')).toEqual([['move', 'move']])
    expect(terms('ultra beast')).toEqual([['category']])
  })

  it('reads every kind of word, in both languages', () => {
    const kinds = (query: string) => parseQuery(query, pokedex).groups[0]?.alternatives[0]?.term
    expect(kinds('fogo')).toEqual({ kind: 'type', type: 'fire' })
    expect(kinds('Elétrico')).toEqual({ kind: 'type', type: 'electric' })
    expect(kinds('gen4')).toEqual({ kind: 'generation', generation: 4 })
    expect(kinds('sinnoh')).toEqual({ kind: 'generation', generation: 4 })
    expect(kinds('hisui')).toEqual({ kind: 'hisui' })
    expect(kinds('hisuian')).toEqual({ kind: 'regional', region: 'hisuian' })
    expect(kinds('sombroso')).toEqual({ kind: 'version', version: 'shadow' })
    expect(kinds('gigantamax')).toEqual({ kind: 'version', version: 'gmax' })
    expect(kinds('lendário')).toEqual({ kind: 'category', category: 'Legendary' })
    expect(kinds('unreleased')).toEqual({ kind: 'released', released: false })
    expect(kinds('445')).toEqual({ kind: 'number', from: 445, to: 445 })
    expect(kinds('#1-151')).toEqual({ kind: 'number', from: 1, to: 151 })
    expect(kinds('387-')).toMatchObject({ kind: 'number', from: 387 })
    expect(kinds('-151')).toEqual({ kind: 'number', from: 1, to: 151 })
    expect(kinds('atk250-')).toEqual({ kind: 'stat', stat: 'attack', min: 250, max: null })
    expect(kinds('def200-300')).toEqual({ kind: 'stat', stat: 'defense', min: 200, max: 300 })
    expect(kinds('sta-150')).toEqual({ kind: 'stat', stat: 'stamina', min: null, max: 150 })
    expect(kinds('PC4000-')).toEqual({ kind: 'stat', stat: 'maxCp', min: 4000, max: null })
    expect(kinds('hp207')).toEqual({ kind: 'stat', stat: 'stamina', min: 207, max: 207 })
    expect(kinds('@1fire')).toMatchObject({ kind: 'moveType', type: 'fire', slot: 'fast' })
    expect(kinds('@2fogo')).toMatchObject({ kind: 'moveType', type: 'fire', slot: 'charged' })
    expect(kinds('@3fire')).toMatchObject({ kind: 'moveType', type: 'fire', slot: 'charged' })
    expect(kinds('@fire')).toMatchObject({ kind: 'moveType', type: 'fire', slot: 'any' })
    expect(kinds('@terremoto')).toMatchObject({ kind: 'move', text: 'terremoto' })
    expect(kinds('+pikachu')).toMatchObject({ kind: 'family', text: 'pikachu' })
    expect(kinds('xyzzy')).toEqual({ kind: 'unknown', text: 'xyzzy' })
    expect(kinds('@xyzzy')).toEqual({ kind: 'unknown', text: '@xyzzy' })
    expect(kinds('+xyzzy')).toEqual({ kind: 'unknown', text: '+xyzzy' })
    expect(kinds('atk')).toEqual({ kind: 'unknown', text: 'atk' })
  })

  it('skips bare symbols while typing, and keeps what was typed', () => {
    expect(parseQuery('! @ + fire', pokedex).groups).toHaveLength(1)
    expect(parseQuery('', pokedex).groups).toEqual([])
    expect(parseQuery('fire,', pokedex).groups[0]?.alternatives).toHaveLength(1)
    expect(parseQuery('!legendary', pokedex).groups[0]?.alternatives[0]).toMatchObject({
      negated: true,
      text: '!legendary',
    })
    expect(parseQuery('mega charizard y', pokedex).groups[0]?.alternatives[0]?.text).toBe(
      'mega charizard y',
    )
    expect(parseQuery('@,fire', pokedex).groups).toHaveLength(1)
  })

  it('lists the stats asked about, not the negated ones', () => {
    expect(queryStats(parseQuery('atk250- !def200- cp3000-', pokedex))).toEqual(['attack', 'maxCp'])
  })
})

describe('searching the Pokédex', () => {
  it('finds names, nicknames and numbers in both languages', () => {
    expect(find('garch')).toEqual([445])
    expect(find('chomp')).toEqual([445])
    expect(find('m2')).toEqual([150])
    expect(find('zard')).toEqual([6])
    expect(find('mr mime')).toEqual([122])
    expect(find('445')).toEqual([445])
    expect(find('1-151')).toEqual([6, 25, 26, 59, 122, 131, 146, 150, 151])
    expect(find('152-')).toEqual([157, 382, 445, 487, 793, 899])
    expect(find('raichu de alola')).toEqual([26])
  })

  it('finds every Fire, Water or Rock Pokémon with commas, and only Fire/Flying with &', () => {
    expect(find('fire,water,rock')).toEqual([6, 59, 131, 146, 157, 382, 793])
    expect(find('fire&flying')).toEqual([6, 146])
    expect(find('fire flying')).toEqual([6, 146])
  })

  it('looks at every version and shows the matching one', () => {
    expect(find('dragon fire')).toEqual([6])
    expect(shown('dragon fire')).toEqual({ 6: 'charizard_mega_x' })
    expect(shown('rock fire')).toEqual({ 59: 'arcanine_hisuian' })
    expect(shown('mega charizard y')).toEqual({ 6: 'charizard_mega_y' })
    expect(shown('primal')).toEqual({ 382: 'kyogre_primal' })
    expect(shown('origin')).toEqual({ 487: 'giratina_origin' })
  })

  it('shows the version with the best value of the stat asked about', () => {
    expect(shown('fire atk230-')).toEqual({
      6: 'charizard_mega_y',
      59: 'arcanine_hisuian',
      146: 'moltres',
      157: 'typhlosion_hisuian',
    })
  })

  it('understands not: about the species for versions and names, about the version for types', () => {
    expect(find('fire !legendary')).toEqual([6, 59, 157])
    expect(find('fire !mega')).toEqual([59, 146, 157])
    expect(find('dragon !flying')).toEqual([6, 445, 487])
    expect(find('electric !pikachu')).toEqual([26])
    expect(find('psychic !shadow')).toEqual([26, 122, 151, 899])
  })

  it('finds moves, move types and families', () => {
    expect(find('@earthquake')).toEqual([445])
    expect(find('@firepunch')).toEqual([6, 445])
    expect(find('@fire punch')).toEqual([6, 445])
    expect(find('@soco de fogo')).toEqual([6, 445])
    expect(find('@fire')).toEqual([6, 59, 445])
    expect(find('@1fire')).toEqual([6, 59])
    expect(find('@2fire')).toEqual([6, 445])
    expect(find('@thunder')).toEqual([25])
    expect(find('@outr')).toEqual([6, 445])
    expect(find('@rage')).toEqual([6, 445])
    expect(find('+pikachu')).toEqual([25, 26])
  })

  it('finds versions, categories, regions and released', () => {
    expect(find('mega')).toEqual([6, 445])
    expect(find('mega shadow')).toEqual([6, 445])
    expect(find('mega&gmax')).toEqual([6])
    expect(shown('mega')).toEqual({ 6: 'charizard_mega_x', 445: 'garchomp_mega' })
    expect(find('shadow')).toEqual([6, 59, 146, 150, 445])
    expect(find('dynamax')).toEqual([6, 25, 131])
    expect(find('gmax')).toEqual([6, 25, 131])
    expect(find('hisuian')).toEqual([59, 157])
    expect(find('alolan')).toEqual([26])
    expect(find('legendary')).toEqual([146, 150, 382, 487])
    expect(find('mythical')).toEqual([151])
    expect(find('mythical', false)).toEqual([151, 493])
    expect(find('ultrabeast')).toEqual([793])
    expect(find('kanto')).toEqual([6, 25, 26, 59, 122, 131, 146, 150, 151])
    expect(find('hisui')).toEqual([899])
    expect(find('unreleased')).toEqual([])
    expect(find('unreleased', false)).toEqual([493])
    expect(find('released', false)).not.toContain(493)
  })

  it('finds nothing for a word it doesn’t know', () => {
    expect(find('xyzzy')).toEqual([])
    expect(find('fire xyzzy')).toEqual([])
    expect(find('fire,xyzzy')).toEqual([6, 59, 146, 157])
  })
})
