import { useDeferredValue, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { $api } from '../../api/client'
import { LoadError } from '../../components/LoadError'
import { useCurrentLanguage, useFormats } from '../../i18n/languageContext'
import { pokedexSections } from '../../layout/nav'
import { TabScreen } from '../../layout/Screen'
import { useIsDesktop } from '../../layout/useMediaQuery'
import { preparePokedex } from '../../pokedex/data'
import {
  countFilters,
  noFilters,
  readFilters,
  stats,
  writeFilters,
  type Filters,
} from '../../pokedex/filters'
import { parseQuery, queryStats, type Stat } from '../../pokedex/search'
import { buildRows, sortRows, type Row } from '../../pokedex/view'
import { FilterBar, PhoneChips } from './FilterBar'
import { FilterSheet } from './FilterSheet'
import { speciesName } from './labels'
import { PokedexList } from './PokedexList'
import { QueryChips } from './QueryChips'
import { SearchBox } from './SearchBox'
import { SearchHelp } from './SearchHelp'
import { SortSheet } from './SortSheet'

/** Browsers keep the Pokédex an hour (GET /pokedex's cache); the app doesn't ask again sooner. */
const pokedexStaleMs = 60 * 60 * 1000

type Open = 'filters' | 'sort' | 'help' | null

/**
 * B1 Pokédex list (E03-F01): every species on one line, searched, filtered and sorted in the app from one download
 * of the whole Pokédex (decision 2026-11-08). Search, filters and sort live in the address.
 */
export function PokedexScreen() {
  const { t } = useTranslation()
  const desktop = useIsDesktop()
  const formats = useFormats()
  const language = useCurrentLanguage()
  const [params, setParams] = useSearchParams()
  const filters = useMemo(() => readFilters(params), [params])
  const [open, setOpen] = useState<Open>(null)
  const query = $api.useQuery('get', '/pokedex', undefined, { staleTime: pokedexStaleMs })
  const pokedex = useMemo(() => (query.data ? preparePokedex(query.data) : null), [query.data])

  // Typing stays smooth: the list catches up with the search box a moment later.
  const q = useDeferredValue(filters.q)
  const parsed = useMemo(() => (pokedex ? parseQuery(q, pokedex) : null), [pokedex, q])
  const rows = useMemo(() => {
    if (!pokedex || !parsed) return null
    const built = buildRows(pokedex, filters, parsed)
    return sortRows(built, filters, (entry) => speciesName(entry, language), language)
  }, [pokedex, parsed, filters, language])
  // The stat that picked the versions shown (the first a filter or the search asks about): coloured on the lines.
  const asked = [
    ...stats.filter((stat) => filters.ranges[stat]),
    ...(parsed ? queryStats(parsed) : []),
  ]
  const strong: Stat | null = stats.find((stat) => asked.includes(stat)) ?? null

  const change = (next: Filters) => void setParams(writeFilters(next), { replace: true })
  const countText = rows
    ? t('pokedex.count', { count: rows.length, formatted: formats.number(rows.length) })
    : null

  const searchBox = (
    <SearchBox
      value={filters.q}
      onChange={(text) => change({ ...filters, q: text })}
      onHelp={() => setOpen('help')}
    />
  )

  return (
    <TabScreen
      title="pokedex"
      sections={{ label: 'pokedex', links: pokedexSections }}
      subtitle={desktop ? countText : undefined}
      searchBox={searchBox}
    >
      <div className="pokedex">
        {desktop ? (
          <FilterBar
            filters={filters}
            searchBox={searchBox}
            onChange={change}
            onOpenFilters={() => setOpen('filters')}
          />
        ) : (
          <PhoneChips
            filters={filters}
            onChange={change}
            onOpenFilters={() => setOpen('filters')}
            onOpenSort={() => setOpen('sort')}
          />
        )}
        {pokedex && <QueryChips query={q} pokedex={pokedex} />}
        {query.isPending ? (
          <p className="pokedex-loading" role="status">
            {t('pokedex.loading')}
          </p>
        ) : !rows ? (
          <LoadError error={query.error} onRetry={() => void query.refetch()} />
        ) : (
          <Results
            rows={rows}
            filters={filters}
            strong={strong}
            countText={desktop ? null : countText}
            onChange={change}
          />
        )}
      </div>
      {open === 'filters' && pokedex && (
        <FilterSheet
          pokedex={pokedex}
          filters={filters}
          onApply={change}
          onClose={() => setOpen(null)}
        />
      )}
      {open === 'sort' && (
        <SortSheet filters={filters} onChange={change} onClose={() => setOpen(null)} />
      )}
      {open === 'help' && (
        <SearchHelp
          onTry={(text) => {
            change({ ...filters, q: text })
            setOpen(null)
          }}
          onClose={() => setOpen(null)}
        />
      )}
    </TabScreen>
  )
}

function Results({
  rows,
  filters,
  strong,
  countText,
  onChange,
}: {
  rows: Row[]
  filters: Filters
  strong: Stat | null
  /** The phone's count above the list; null on the desktop (it's next to the title). */
  countText: string | null
  onChange: (filters: Filters) => void
}) {
  const { t } = useTranslation()
  const narrowed = filters.q.trim() !== '' || countFilters(filters) > 0

  if (!rows.length) {
    return (
      <section className="pokedex-empty ui-card ui-card-padded" aria-live="polite">
        <h2>{t('pokedex.empty.title')}</h2>
        <p>{t('pokedex.empty.text')}</p>
        {filters.releasedOnly && <p>{t('pokedex.empty.releasedOnly')}</p>}
        <button type="button" className="button" onClick={() => onChange(noFilters)}>
          {t('pokedex.empty.clear')}
        </button>
      </section>
    )
  }

  return (
    <>
      {countText !== null && (
        <div className="pokedex-legend">
          <span aria-live="polite">{countText}</span>
          <span className="pokedex-legend-stats">
            <span>{t('pokedex.legendCp')}</span>
            <span>{t('pokedex.legendStats')}</span>
          </span>
        </div>
      )}
      <PokedexList rows={rows} filters={filters} strong={strong} onSort={onChange} />
      {narrowed && <p className="pokedex-footnote">{t('pokedex.footnote')}</p>}
    </>
  )
}
