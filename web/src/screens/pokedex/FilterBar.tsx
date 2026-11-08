import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '../../components/icons'
import { useFormats } from '../../i18n/languageContext'
import { regionsByGeneration } from '../../pokedex/data'
import { countFilters, stats, type Filters, type VersionFilter } from '../../pokedex/filters'
import { Chip } from '../../ui/Chip'
import { rangeLabel } from './labels'

/** The quick toggles next to the filters (B1): Released, Has Mega, Has Shadow. */
function QuickToggles({
  filters,
  onChange,
}: {
  filters: Filters
  onChange: (filters: Filters) => void
}) {
  const { t } = useTranslation()
  const toggle = (version: VersionFilter) =>
    onChange({
      ...filters,
      versions: filters.versions.includes(version)
        ? filters.versions.filter((v) => v !== version)
        : [...filters.versions, version],
    })
  return (
    <>
      <Chip
        selected={filters.releasedOnly}
        onClick={() => onChange({ ...filters, releasedOnly: !filters.releasedOnly })}
      >
        {t('pokedex.released')}
      </Chip>
      <Chip selected={filters.versions.includes('mega')} onClick={() => toggle('mega')}>
        {t('pokedex.has.mega')}
      </Chip>
      <Chip selected={filters.versions.includes('shadow')} onClick={() => toggle('shadow')}>
        {t('pokedex.has.shadow')}
      </Chip>
    </>
  )
}

/** The phone's row of chips under the search box: Filters, Sort, Released, Has Mega, Has Shadow (scrolls sideways). */
export function PhoneChips({
  filters,
  onChange,
  onOpenFilters,
  onOpenSort,
}: {
  filters: Filters
  onChange: (filters: Filters) => void
  onOpenFilters: () => void
  onOpenSort: () => void
}) {
  const { t } = useTranslation()
  const count = countFilters(filters)
  const arrow = filters.order === 'asc' ? '↑' : '↓'
  return (
    <div className="pokedex-chips">
      <Chip selected={count > 0} onClick={onOpenFilters}>
        <Icon name="filters" size={16} />
        {count ? t('pokedex.filtersCount', { count }) : t('pokedex.filters')}
      </Chip>
      <Chip onClick={onOpenSort}>
        {t('pokedex.sortChip', { column: t(`pokedex.columns.${filters.sort}`) })} {arrow}
      </Chip>
      <QuickToggles filters={filters} onChange={onChange} />
    </div>
  )
}

/** The filters that are set, as chips with ✕ (desktop bar, B1x): "Type: Fire ✕", "Attack ≥ 230 ✕". */
function SetFilters({
  filters,
  onChange,
}: {
  filters: Filters
  onChange: (filters: Filters) => void
}) {
  const { t } = useTranslation()
  const formats = useFormats()
  const chips: { key: string; label: string; clear: () => Filters }[] = []
  if (filters.types.length) {
    chips.push({
      key: 'type',
      label: `${t('pokedex.sheet.type')}: ${filters.types.map((type) => t(`types.${type}`)).join(' + ')}`,
      clear: () => ({ ...filters, types: [] }),
    })
  }
  if (filters.generations.length) {
    chips.push({
      key: 'gen',
      label: filters.generations
        .map((gen) => t(`pokedex.regions.${regionsByGeneration[gen - 1] ?? 'kanto'}`))
        .join(', '),
      clear: () => ({ ...filters, generations: [] }),
    })
  }
  for (const stat of stats) {
    const range = filters.ranges[stat]
    if (!range) continue
    chips.push({
      key: stat,
      label: rangeLabel(stat, range, t, formats),
      clear: () => {
        const ranges = { ...filters.ranges }
        delete ranges[stat]
        return { ...filters, ranges }
      },
    })
  }
  for (const version of filters.versions.filter((v) => v !== 'mega' && v !== 'shadow')) {
    chips.push({
      key: version,
      label: t(`pokedex.has.${version}`),
      clear: () => ({ ...filters, versions: filters.versions.filter((v) => v !== version) }),
    })
  }
  for (const category of filters.categories) {
    chips.push({
      key: category,
      label: t(`pokedex.categories.${category}`),
      clear: () => ({ ...filters, categories: filters.categories.filter((c) => c !== category) }),
    })
  }
  return (
    <>
      {chips.map((chip) => (
        <Chip
          key={chip.key}
          onRemove={() => onChange(chip.clear())}
          removeLabel={t('pokedex.set.remove', { name: chip.label })}
        >
          {chip.label}
        </Chip>
      ))}
    </>
  )
}

/** The desktop's filter bar (B1): search box, Type, Generation, More filters, the set filters, then the toggles. */
export function FilterBar({
  filters,
  searchBox,
  onChange,
  onOpenFilters,
}: {
  filters: Filters
  searchBox: ReactNode
  onChange: (filters: Filters) => void
  onOpenFilters: () => void
}) {
  const { t } = useTranslation()
  return (
    <div className="pokedex-bar">
      <div className="pokedex-bar-search">{searchBox}</div>
      {!filters.types.length && (
        <Chip onClick={onOpenFilters}>
          {t('pokedex.typeAny')}{' '}
          <span className="pokedex-caret">
            <Icon name="chevron" size={14} />
          </span>
        </Chip>
      )}
      {!filters.generations.length && (
        <Chip onClick={onOpenFilters}>
          {t('pokedex.generationAll')}{' '}
          <span className="pokedex-caret">
            <Icon name="chevron" size={14} />
          </span>
        </Chip>
      )}
      <SetFilters filters={filters} onChange={onChange} />
      <Chip onClick={onOpenFilters}>
        <Icon name="filters" size={16} />
        {t('pokedex.moreFilters')}
      </Chip>
      <span className="pokedex-bar-end">
        <QuickToggles filters={filters} onChange={onChange} />
      </span>
    </div>
  )
}
