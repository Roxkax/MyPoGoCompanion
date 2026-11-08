import { describe, expect, it } from 'vitest'
import { samplePokedex } from '../testing/pokedex'
import { preparePokedex, type Entry } from './data'
import {
  countFilters,
  defaultOrder,
  noFilters,
  readFilters,
  sortBy,
  writeFilters,
  type Filters,
} from './filters'
import { emptyQuery, parseQuery } from './search'
import { buildRows, markersFor, pickVersion, sortRows, versionsInPlay, type Marker } from './view'

const pokedex = preparePokedex(samplePokedex)
const entry = (dex: number) => pokedex.entries.find((e) => e.dex === dex)!

function rows(filters: Partial<Filters>, query = '') {
  return buildRows(pokedex, { ...noFilters, ...filters }, parseQuery(query, pokedex))
}

function dexes(filters: Partial<Filters>, query = '') {
  return rows(filters, query).map((row) => row.entry.dex)
}

function describeMarkers(markers: Marker[]): string[] {
  return markers.map((marker) =>
    marker.kind === 'forms'
      ? `forms:${marker.count}`
      : 'version' in marker
        ? `${marker.kind}:${marker.version.id}`
        : marker.kind,
  )
}

describe('filters in the address', () => {
  it('reads every filter and ignores what it doesn’t know', () => {
    const filters = readFilters(
      new URLSearchParams(
        'q=fire,water&type=fire,FLYING,fire,nope,ice&gen=4,1,10,x&atk=230-&def=-200&sta=207&cp=x&has=mega,regional,nope' +
          '&cat=legendary,ultrabeast,nope&released=all&sort=atk&order=asc',
      ),
    )
    expect(filters).toEqual({
      q: 'fire,water',
      types: ['fire', 'flying'],
      generations: [1, 4],
      ranges: {
        attack: { min: 230, max: null },
        defense: { min: null, max: 200 },
        stamina: { min: 207, max: 207 },
      },
      versions: ['mega', 'regional'],
      categories: ['Legendary', 'UltraBeast'],
      releasedOnly: false,
      sort: 'attack',
      order: 'asc',
    })
  })

  it('starts with no filters, released only, in Pokédex order', () => {
    expect(readFilters(new URLSearchParams(''))).toEqual(noFilters)
    expect(readFilters(new URLSearchParams('sort=nope&order=up&atk=-'))).toEqual(noFilters)
    expect(readFilters(new URLSearchParams('sort=cp')).order).toBe('desc')
  })

  it('writes only what differs, and reads back the same', () => {
    expect(writeFilters(noFilters).toString()).toBe('')
    const filters: Filters = {
      q: 'mega',
      types: ['fire'],
      generations: [1],
      ranges: {
        attack: { min: 230, max: null },
        stamina: { min: 207, max: 207 },
        maxCp: { min: null, max: null },
      },
      versions: ['shadow'],
      categories: ['Mythical'],
      releasedOnly: false,
      sort: 'name',
      order: 'desc',
    }
    const written = writeFilters(filters)
    expect(written.toString()).toBe(
      'q=mega&type=fire&gen=1&atk=230-&sta=207&has=shadow&cat=mythical&released=all&sort=name&order=desc',
    )
    expect(readFilters(written)).toEqual({
      ...filters,
      ranges: { attack: filters.ranges.attack, stamina: filters.ranges.stamina },
    })
  })

  it('counts the filters that are set', () => {
    expect(countFilters(noFilters)).toBe(0)
    expect(
      countFilters({
        ...noFilters,
        types: ['fire', 'flying'],
        generations: [1],
        ranges: { attack: { min: 1, max: null } },
        versions: ['mega'],
        categories: ['Mythical'],
      }),
    ).toBe(6)
  })

  it('sorts by a column, and reverses on the second tap', () => {
    expect(defaultOrder('number')).toBe('asc')
    expect(defaultOrder('attack')).toBe('desc')
    const byAttack = sortBy(noFilters, 'attack')
    expect([byAttack.sort, byAttack.order]).toEqual(['attack', 'desc'])
    expect(sortBy(byAttack, 'attack').order).toBe('asc')
    expect(sortBy(sortBy(byAttack, 'attack'), 'attack').order).toBe('desc')
    expect(sortBy(byAttack, 'name').order).toBe('asc')
  })
})

describe('the list', () => {
  it('has a line per species with something released, showing the main form', () => {
    const list = rows({})
    expect(list.map((row) => row.entry.dex)).not.toContain(493)
    expect(list.every((row) => row.shown.isDefault)).toBe(true)
    expect(dexes({ releasedOnly: false })).toContain(493)
  })

  it('filters by type (both types needed), generation, category and stats, version by version', () => {
    expect(dexes({ types: ['fire'] })).toEqual([6, 59, 146, 157])
    expect(dexes({ types: ['fire', 'dragon'] })).toEqual([6])
    expect(dexes({ generations: [2, 3] })).toEqual([157, 382])
    expect(dexes({ categories: ['Mythical', 'UltraBeast'] })).toEqual([151, 793])
    expect(dexes({ ranges: { attack: { min: 300, max: null } } })).toEqual([6, 150, 382, 445])
    expect(dexes({ ranges: { maxCp: { min: null, max: 1100 } } })).toEqual([25])
    expect(dexes({ types: ['fire'], ranges: { attack: { min: 230, max: null } } })).toEqual([
      6, 59, 146, 157,
    ])
  })

  it('filters by the versions a Pokémon has', () => {
    expect(dexes({ versions: ['mega'] })).toEqual([6, 382, 445])
    expect(dexes({ versions: ['shadow'] })).toEqual([6, 59, 146, 150, 445])
    expect(dexes({ versions: ['dynamax'] })).toEqual([6, 25, 131])
    expect(dexes({ versions: ['gmax'] })).toEqual([6, 25, 131])
    expect(dexes({ versions: ['regional'] })).toEqual([26, 59, 157])
  })

  it('combines version filters across the species’ versions (a Mega and a Shadow)', () => {
    expect(dexes({ versions: ['mega', 'shadow'] })).toEqual([6, 445])
    expect(dexes({ versions: ['regional', 'shadow'] })).toEqual([59])
    expect(dexes({ versions: ['mega', 'gmax'] })).toEqual([6])
  })

  it('shows the best version for the first stat asked about, else the main form, else the first match', () => {
    const list = rows({
      types: ['fire'],
      ranges: { attack: { min: 230, max: null }, maxCp: { min: 1, max: null } },
    })
    expect(list.find((row) => row.entry.dex === 6)?.shown.id).toBe('charizard_mega_y')
    expect(rows({}, 'cp4000-').find((row) => row.entry.dex === 150)?.shown.id).toBe('mewtwo')
    // The filter chips list the Pokémon as they are; a version word in the search shows that version.
    expect(rows({ versions: ['regional'] }).find((row) => row.entry.dex === 26)?.shown.id).toBe(
      'raichu',
    )
    expect(rows({}, 'alolan').find((row) => row.entry.dex === 26)?.shown.id).toBe('raichu_alolan')
    const [main, ...others] = entry(6).versions
    expect(pickVersion([main!, ...others], ['defense']).id).toBe('charizard_mega_x')
  })

  it('combines the filters and the search', () => {
    expect(dexes({ types: ['fire'] }, 'legendary')).toEqual([146])
    expect(dexes({ versions: ['mega'] }, '!legendary')).toEqual([6, 445])
  })

  it('only counts released versions unless “Released” is off', () => {
    expect(versionsInPlay(entry(493), true)).toEqual([])
    expect(versionsInPlay(entry(493), false)).toHaveLength(6)
  })
})

describe('markers', () => {
  it('lists Megas, regional forms and other forms, then Shadow, Dynamax and G-Max', () => {
    expect(describeMarkers(markersFor(entry(6).versions))).toEqual([
      'mega:charizard_mega_x',
      'mega:charizard_mega_y',
      'shadow',
      'dynamax',
      'gmax',
    ])
    expect(describeMarkers(markersFor(entry(382).versions))).toEqual(['primal:kyogre_primal'])
    expect(describeMarkers(markersFor(entry(59).versions))).toEqual([
      'regional:arcanine_hisuian',
      'shadow',
    ])
    expect(describeMarkers(markersFor(entry(150).versions))).toEqual([
      'form:mewtwo_armored',
      'shadow',
    ])
  })

  it('leaves out looks-only forms, and groups more than four other forms', () => {
    expect(describeMarkers(markersFor(entry(25).versions))).toEqual(['dynamax', 'gmax'])
    expect(describeMarkers(markersFor(entry(493).versions))).toEqual(['forms:5'])
  })

  it('hides the markers of versions that don’t match', () => {
    const charizard = rows({}, 'dragon').find((row) => row.entry.dex === 6)!
    expect(describeMarkers(charizard.markers)).toEqual(['mega:charizard_mega_x'])
  })
})

describe('sorting', () => {
  const name = (e: Entry) => e.nameEn
  const order = (filters: Partial<Filters>) =>
    sortRows(
      buildRows(pokedex, { ...noFilters, ...filters }, emptyQuery),
      { ...noFilters, ...filters },
      name,
      'en',
    ).map((row) => row.entry.dex)

  it('sorts by number, name or the numbers shown, ties in Pokédex order', () => {
    expect(order({ sort: 'number', order: 'desc' }).slice(0, 2)).toEqual([899, 793])
    expect(order({ sort: 'name', order: 'asc' }).slice(0, 3)).toEqual([59, 6, 445])
    expect(order({ sort: 'attack', order: 'desc' }).slice(0, 2)).toEqual([150, 382])
    expect(order({ sort: 'maxCp', order: 'asc' })[0]).toBe(25)
    // Same Attack (223): Charizard before Typhlosion.
    const attack = order({ sort: 'attack', order: 'asc' })
    expect(attack.indexOf(6)).toBeLessThan(attack.indexOf(157))
  })

  it('sorts by the version shown', () => {
    const filters: Filters = {
      ...noFilters,
      ranges: { attack: { min: 300, max: null } },
      sort: 'attack',
      order: 'desc',
    }
    const sorted = sortRows(buildRows(pokedex, filters, emptyQuery), filters, name, 'en')
    expect(sorted.map((row) => row.shown.id)).toEqual([
      'kyogre_primal',
      'garchomp_mega',
      'charizard_mega_y',
      'mewtwo',
    ])
  })
})
