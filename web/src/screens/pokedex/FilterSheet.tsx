import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useFormats } from '../../i18n/languageContext'
import type { Pokedex } from '../../pokedex/data'
import {
  categories,
  noFilters,
  stats,
  versionFilters,
  type Filters,
  type StatRange,
} from '../../pokedex/filters'
import { parseQuery, type Stat } from '../../pokedex/search'
import { buildRows } from '../../pokedex/view'
import { Chip } from '../../ui/Chip'
import { Sheet } from '../../ui/Sheet'
import { GenerationChoices, TypeChoices } from './FilterChoices'
import { statLabel } from './labels'

/** The lowest and highest value of each stat over every version, the sliders' ends. */
function statBounds(pokedex: Pokedex): Record<Stat, { low: number; high: number }> {
  const bounds = Object.fromEntries(
    stats.map((stat) => [stat, { low: Infinity, high: 0 }]),
  ) as Record<Stat, { low: number; high: number }>
  for (const entry of pokedex.entries) {
    for (const version of entry.versions) {
      for (const stat of stats) {
        bounds[stat].low = Math.min(bounds[stat].low, version[stat])
        bounds[stat].high = Math.max(bounds[stat].high, version[stat])
      }
    }
  }
  for (const stat of stats) {
    if (bounds[stat].low > bounds[stat].high) bounds[stat] = { low: 0, high: 0 }
  }
  return bounds
}

function toggle<T>(list: readonly T[], value: T): T[] {
  return list.includes(value) ? list.filter((item) => item !== value) : [...list, value]
}

/** Two thumbs on one track: the lowest and highest value of a stat. At an end, that side has no limit. */
function RangeSlider({
  stat,
  low,
  high,
  range,
  onChange,
}: {
  stat: Stat
  low: number
  high: number
  range: StatRange | undefined
  onChange: (range: StatRange | undefined) => void
}) {
  const { t } = useTranslation()
  const formats = useFormats()
  const min = range?.min ?? low
  const max = range?.max ?? high
  const name = statLabel(stat, t)
  const set = (nextMin: number, nextMax: number) => {
    const from = nextMin <= low ? null : nextMin
    const to = nextMax >= high ? null : nextMax
    onChange(from === null && to === null ? undefined : { min: from, max: to })
  }
  const span = Math.max(1, high - low)
  return (
    <div className="range">
      <div className="range-head">
        <span className="range-name">{name}</span>
        <span className="range-values num">
          {formats.number(min)} – {formats.number(max)}
        </span>
      </div>
      <div
        className="range-track"
        style={{
          ['--from' as string]: `${((min - low) / span) * 100}%`,
          ['--to' as string]: `${((max - low) / span) * 100}%`,
        }}
      >
        <input
          type="range"
          min={low}
          max={high}
          value={min}
          aria-label={t('pokedex.sheet.from', { stat: name })}
          onChange={(event) => set(Math.min(Number(event.target.value), max), max)}
        />
        <input
          type="range"
          min={low}
          max={high}
          value={max}
          aria-label={t('pokedex.sheet.to', { stat: name })}
          onChange={(event) => set(min, Math.max(Number(event.target.value), min))}
        />
      </div>
    </div>
  )
}

/**
 * B1's filter sheet (phone) and "More filters" window (desktop): type, stats, generation, versions, category. It
 * works on a copy; "Show N Pokémon" applies it, counting as you change things.
 */
export function FilterSheet({
  pokedex,
  filters,
  part = 'all',
  onApply,
  onClose,
}: {
  pokedex: Pokedex
  filters: Filters
  /** `more`: the desktop's "More filters", without Type and Generation (they have their own chips in the bar). */
  part?: 'all' | 'more'
  onApply: (filters: Filters) => void
  onClose: () => void
}) {
  const { t } = useTranslation()
  const formats = useFormats()
  const [draft, setDraft] = useState(filters)
  const bounds = useMemo(() => statBounds(pokedex), [pokedex])
  const query = useMemo(() => parseQuery(draft.q, pokedex), [draft.q, pokedex])
  const count = useMemo(() => buildRows(pokedex, draft, query).length, [pokedex, draft, query])
  const all = part === 'all'
  const cleared: Filters = all
    ? { ...noFilters, q: draft.q, sort: draft.sort, order: draft.order }
    : { ...draft, ranges: {}, versions: [], categories: [], releasedOnly: true }

  return (
    <Sheet
      id="pokedex-filters"
      title={all ? t('pokedex.sheet.title') : t('pokedex.moreFilters')}
      onClose={onClose}
      wide
      headerEnd={
        <button type="button" className="link-button" onClick={() => setDraft(cleared)}>
          {t('pokedex.sheet.clearAll')}
        </button>
      }
      footer={
        <>
          <button
            type="button"
            className="button button-secondary"
            onClick={() => setDraft(filters)}
          >
            {t('pokedex.sheet.reset')}
          </button>
          <button
            type="button"
            className="button button-wide"
            onClick={() => {
              onApply(draft)
              onClose()
            }}
          >
            {t('pokedex.sheet.apply', { count, formatted: formats.number(count) })}
          </button>
        </>
      }
    >
      {all && (
        <section className="filter-group" aria-labelledby="filter-type">
          <h3 id="filter-type">{t('pokedex.sheet.type')}</h3>
          <TypeChoices types={draft.types} onChange={(types) => setDraft({ ...draft, types })} />
        </section>
      )}

      <section className="filter-group" aria-labelledby="filter-stats">
        <h3 id="filter-stats">{t('pokedex.sheet.stats')}</h3>
        {stats.map((stat) => (
          <RangeSlider
            key={stat}
            stat={stat}
            low={bounds[stat].low}
            high={bounds[stat].high}
            range={draft.ranges[stat]}
            onChange={(range) => {
              const ranges = { ...draft.ranges }
              if (range) ranges[stat] = range
              else delete ranges[stat]
              setDraft({ ...draft, ranges })
            }}
          />
        ))}
      </section>

      {all && (
        <section className="filter-group" aria-labelledby="filter-generation">
          <h3 id="filter-generation">{t('pokedex.sheet.generation')}</h3>
          <GenerationChoices
            generations={draft.generations}
            onChange={(generations) => setDraft({ ...draft, generations })}
          />
        </section>
      )}

      <section className="filter-group" aria-labelledby="filter-show">
        <h3 id="filter-show">{t('pokedex.sheet.show')}</h3>
        <div className="wrap-row">
          <Chip
            selected={draft.releasedOnly}
            onClick={() => setDraft({ ...draft, releasedOnly: !draft.releasedOnly })}
          >
            {t('pokedex.releasedOnly')}
          </Chip>
          {versionFilters.map((version) => (
            <Chip
              key={version}
              selected={draft.versions.includes(version)}
              onClick={() => setDraft({ ...draft, versions: toggle(draft.versions, version) })}
            >
              {t(`pokedex.has.${version}`)}
            </Chip>
          ))}
        </div>
      </section>

      <section className="filter-group" aria-labelledby="filter-category">
        <h3 id="filter-category">{t('pokedex.sheet.category')}</h3>
        <div className="wrap-row">
          {categories.map((category) => (
            <Chip
              key={category}
              selected={draft.categories.includes(category)}
              onClick={() => setDraft({ ...draft, categories: toggle(draft.categories, category) })}
            >
              {t(`pokedex.categories.${category}`)}
            </Chip>
          ))}
        </div>
      </section>
    </Sheet>
  )
}
