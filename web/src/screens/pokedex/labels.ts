import type { TFunction } from 'i18next'
import type { Formats } from '../../i18n/format'
import type { Language } from '../../i18n/languages'
import { regionsByGeneration, type Entry, type Version } from '../../pokedex/data'
import type { StatRange } from '../../pokedex/filters'
import { normalize } from '../../pokedex/normalize'
import type { Alternative, Stat, Term } from '../../pokedex/search'
import type { Marker } from '../../pokedex/view'

// The texts the Pokédex list shows for its data, in the app's language: names from the game's Portuguese text
// when there is one (English otherwise), markers, the chips that show how the search was read.

export function speciesName(entry: Entry, language: Language): string {
  return (language === 'pt-PT' ? entry.namePt : null) ?? entry.nameEn
}

export function versionName(version: Version, language: Language): string {
  return (language === 'pt-PT' ? version.namePt : null) ?? version.nameEn
}

/** The Pokémon page's address for a line: `/pokedex/mr-mime`, with `?version=` when it shows another version. */
export function pokemonPath(entry: Entry, version?: Version): string {
  const slug = normalize(entry.nameEn).replace(/ /g, '-')
  return version && !version.isDefault
    ? `/pokedex/${slug}?version=${version.id}`
    : `/pokedex/${slug}`
}

/** A marker's text: "Mega Y", "Hisuian", "Origin", "17 forms", "Shadow". */
export function markerLabel(marker: Marker, language: Language, t: TFunction): string {
  switch (marker.kind) {
    case 'mega':
    case 'primal':
    case 'regional':
    case 'form': {
      const label = (language === 'pt-PT' ? marker.version.labelPt : null) ?? marker.version.labelEn
      if (label) return label
      return marker.kind === 'regional'
        ? t(`versions.${marker.region}`)
        : marker.kind === 'form'
          ? versionName(marker.version, language)
          : t(`versions.${marker.kind}`)
    }
    case 'forms':
      return t('pokedex.forms', { count: marker.count })
    default:
      return t(`versions.${marker.kind}`)
  }
}

export function statLabel(stat: Stat, t: TFunction): string {
  return t(`pokedex.columns.${stat}`)
}

/** "Attack ≥ 230", "Stamina ≤ 150", "Defense 200–300". */
export function rangeLabel(stat: Stat, range: StatRange, t: TFunction, formats: Formats): string {
  const name = stat === 'maxCp' ? t('pokedex.columns.maxCp') : statLabel(stat, t)
  const min = range.min === null ? '' : formats.number(range.min)
  const max = range.max === null ? '' : formats.number(range.max)
  if (range.min !== null && range.max !== null) {
    return range.min === range.max
      ? `${name} = ${min}`
      : t('pokedex.set.between', { stat: name, min, max })
  }
  return range.min !== null
    ? t('pokedex.set.atLeast', { stat: name, min })
    : t('pokedex.set.atMost', { stat: name, max })
}

/** How the search read one term. */
export function termLabel(
  term: Term,
  t: TFunction,
  formats: Formats,
  movesName: (id: string) => string,
): string {
  switch (term.kind) {
    case 'name':
      return t('pokedex.terms.name', { text: term.text })
    case 'number':
      if (term.from === term.to) return t('pokedex.terms.number', { from: term.from })
      if (term.to >= Number.MAX_SAFE_INTEGER)
        return t('pokedex.terms.numberFrom', { from: term.from })
      return t('pokedex.terms.numberRange', { from: term.from, to: term.to })
    case 'type':
      return t('pokedex.terms.type', { type: t(`types.${term.type}`) })
    case 'generation': {
      const region = regionsByGeneration[term.generation - 1] ?? 'kanto'
      return t('pokedex.terms.generation', {
        region: t(`pokedex.regions.${region}`),
        generation: term.generation,
      })
    }
    case 'hisui':
      return t('pokedex.terms.hisui')
    case 'regional':
      return t('pokedex.terms.regional', { region: t(`versions.${term.region}`) })
    case 'version':
      return t(`pokedex.terms.${term.version}`)
    case 'category':
      return t(`pokedex.categories.${term.category}`)
    case 'released':
      return t(term.released ? 'pokedex.terms.released' : 'pokedex.terms.unreleased')
    case 'move': {
      const [only] = term.ids
      return term.ids.size === 1 && only
        ? t('pokedex.terms.move', { name: movesName(only) })
        : t('pokedex.terms.moves', { text: term.text, count: term.ids.size })
    }
    case 'moveType': {
      const type = t(`types.${term.type}`)
      return term.slot === 'fast'
        ? t('pokedex.terms.moveFast', { type })
        : term.slot === 'charged'
          ? t('pokedex.terms.moveCharged', { type })
          : t('pokedex.terms.moveAny', { type })
    }
    case 'family':
      return t('pokedex.terms.family', { text: term.text })
    case 'stat':
      return rangeLabel(term.stat, { min: term.min, max: term.max }, t, formats)
    case 'unknown':
      return t('pokedex.terms.unknown', { text: term.text })
  }
}

export function alternativeLabel(
  alternative: Alternative,
  t: TFunction,
  formats: Formats,
  movesName: (id: string) => string,
): string {
  const label = termLabel(alternative.term, t, formats, movesName)
  return alternative.negated ? t('pokedex.not', { term: label }) : label
}
