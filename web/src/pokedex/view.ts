import type { Entry, Pokedex, Region, Version } from './data'
import { stats, type Filters, type VersionFilter } from './filters'
import { queryMatches, queryStats, versionHas, type Query, type Stat } from './search'

/*
 * What the Pokédex list shows (decisions 2026-11-08): one line per species; a line appears when one of its versions
 * matches every filter and the search; it shows the best-matching version and only the markers of matching versions.
 */

/** A marker on a line. Mega, Primal, regional and other forms switch the line; Shadow, Dynamax, G-Max show a note. */
export type Marker =
  | { kind: 'mega' | 'primal'; version: Version }
  | { kind: 'regional'; region: Region; version: Version }
  | { kind: 'form'; version: Version }
  | { kind: 'forms'; count: number }
  | { kind: 'shadow' | 'dynamax' | 'gmax' }

export interface Row {
  entry: Entry
  /** The version the line shows (the main form, unless filters or the search pick another). */
  shown: Version
  markers: Marker[]
}

/** A species with more forms of its own than this gets one "N forms" marker (Arceus, Silvally, Rotom…). */
export const maxFormMarkers = 4

/** The stats that pick the version a line shows, in that order (decision 2026-11-08). */
const statOrder: readonly Stat[] = stats

function versionMatchesFilter(filter: VersionFilter, version: Version): boolean {
  switch (filter) {
    case 'mega':
      return version.kind === 'Mega' || version.kind === 'Primal'
    case 'shadow':
      return version.shadow
    case 'dynamax':
      return version.dynamax
    case 'gmax':
      return version.gigantamax
    case 'regional':
      return version.group === 'regional'
  }
}

/**
 * Whether a version passes the filters (the search is checked separately). The version filters (Has Mega, Has Shadow,
 * Can Dynamax, Can Gigantamax, Regional forms) are about the whole species: see {@link speciesHasVersions}.
 */
export function filtersMatch(filters: Filters, entry: Entry, version: Version): boolean {
  if (filters.generations.length && !filters.generations.includes(entry.generation)) return false
  if (
    filters.categories.length &&
    !(filters.categories as readonly string[]).includes(entry.category)
  )
    return false
  if (!filters.types.every((type) => version.types.includes(type))) return false
  for (const stat of statOrder) {
    const range = filters.ranges[stat]
    if (!range) continue
    const value = version[stat]
    if ((range.min !== null && value < range.min) || (range.max !== null && value > range.max))
      return false
  }
  return true
}

/**
 * Whether the species has every version the filters ask for, each on any of its versions in play: Charizard has a
 * Mega and a Shadow, so "Has Mega" + "Has Shadow" finds it even though no single version is both.
 */
export function speciesHasVersions(filters: Filters, versions: readonly Version[]): boolean {
  return filters.versions.every((filter) =>
    versions.some((version) => versionMatchesFilter(filter, version)),
  )
}

/**
 * The versions the search names by kind ("mega", "primal", "hisuian"): a line shows one of them when it matches, so
 * "mega" shows Mega Charizard X rather than Charizard. The filter chips don't: "Has Mega" lists the Pokémon as they are.
 */
export function namedByQuery(query: Query): (version: Version) => boolean {
  const tests = query.groups.flatMap((group) =>
    group.alternatives.flatMap(({ term, negated }) =>
      !negated && (term.kind === 'version' || term.kind === 'regional') ? [term] : [],
    ),
  )
  return (version) => tests.some((term) => versionHas(term, version))
}

/** The versions a line can show: released ones only, unless "Released" is off. */
export function versionsInPlay(entry: Entry, releasedOnly: boolean): Version[] {
  return releasedOnly ? entry.versions.filter((version) => version.released) : entry.versions
}

/**
 * The version a line shows among those that match: the highest value of the first stat asked about (Attack, Defense,
 * Stamina, Max CP); else a version the search names by kind ("mega"); else the main form when it matches; else the
 * first match (Megas, regional, other forms).
 */
export function pickVersion(
  matching: readonly [Version, ...Version[]],
  asked: readonly Stat[],
  named: (version: Version) => boolean = () => false,
): Version {
  const stat = statOrder.find((candidate) => asked.includes(candidate))
  if (stat) {
    return matching.reduce((best, version) => (version[stat] > best[stat] ? version : best))
  }
  return matching.find(named) ?? matching.find((version) => version.isDefault) ?? matching[0]
}

/** The markers for these versions: switching markers first (Megas, regional, forms), then Shadow, Dynamax, G-Max. */
export function markersFor(versions: readonly Version[]): Marker[] {
  const markers: Marker[] = []
  for (const version of versions) {
    if (version.kind === 'Mega') markers.push({ kind: 'mega', version })
    else if (version.kind === 'Primal') markers.push({ kind: 'primal', version })
  }
  for (const version of versions) {
    if (version.group === 'regional' && version.region) {
      markers.push({ kind: 'regional', region: version.region, version })
    }
  }
  const others = versions.filter((version) => version.group === 'other')
  if (others.length > maxFormMarkers) markers.push({ kind: 'forms', count: others.length })
  else markers.push(...others.map((version): Marker => ({ kind: 'form', version })))
  if (versions.some((version) => version.shadow)) markers.push({ kind: 'shadow' })
  if (versions.some((version) => version.dynamax)) markers.push({ kind: 'dynamax' })
  if (versions.some((version) => version.gigantamax)) markers.push({ kind: 'gmax' })
  return markers
}

/** The lines for these filters and search, in no particular order (see {@link sortRows}). */
export function buildRows(pokedex: Pokedex, filters: Filters, query: Query): Row[] {
  const asked = [...statOrder.filter((stat) => filters.ranges[stat]), ...queryStats(query)]
  const named = namedByQuery(query)
  const rows: Row[] = []
  for (const entry of pokedex.entries) {
    const inPlay = versionsInPlay(entry, filters.releasedOnly)
    if (!speciesHasVersions(filters, inPlay)) continue
    const matching = inPlay.filter(
      (version) =>
        filtersMatch(filters, entry, version) && queryMatches(query, entry, version, inPlay),
    )
    const [first, ...rest] = matching
    if (!first) continue
    rows.push({
      entry,
      shown: pickVersion([first, ...rest], asked, named),
      markers: markersFor(matching),
    })
  }
  return rows
}

/** Sorts lines by the numbers they show; ties keep Pokédex order. `name` gives each line's name in the app's language. */
export function sortRows(
  rows: Row[],
  filters: Filters,
  name: (entry: Entry) => string,
  locale: string,
): Row[] {
  const direction = filters.order === 'asc' ? 1 : -1
  const collator = new Intl.Collator(locale, { sensitivity: 'base', numeric: true })
  const value = (row: Row): number | string =>
    filters.sort === 'number'
      ? row.entry.dex
      : filters.sort === 'name'
        ? name(row.entry)
        : row.shown[filters.sort]
  return [...rows].sort((a, b) => {
    const x = value(a)
    const y = value(b)
    const compared = typeof x === 'string' ? collator.compare(x, y as string) : x - (y as number)
    return compared !== 0 ? compared * direction : a.entry.dex - b.entry.dex
  })
}
