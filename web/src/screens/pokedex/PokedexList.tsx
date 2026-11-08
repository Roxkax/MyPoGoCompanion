import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { useCurrentLanguage, useFormats } from '../../i18n/languageContext'
import { useIsDesktop } from '../../layout/useMediaQuery'
import type { Version } from '../../pokedex/data'
import { sortBy, stats, type Filters, type SortKey } from '../../pokedex/filters'
import type { Stat } from '../../pokedex/search'
import type { Marker, Row } from '../../pokedex/view'
import { pictureSources } from '../../ui/sprites'
import { SpriteBox } from '../../ui/SpriteBox'
import { TypeBadge } from '../../ui/TypeBadge'
import { markerLabel, pokemonPath, speciesName, versionName } from './labels'

type Note = 'shadow' | 'dynamax' | 'gmax'

/**
 * A line's markers (decision 2026-11-08). Hovering (desktop) or tapping (phone) a Mega, regional or other form
 * switches the line to it; tapping again goes back. Shadow, Dynamax and G-Max have the same stats, so they show a
 * note instead. "N forms" opens the Pokémon page.
 */
function Markers({ row, line }: { row: Row; line: Line }) {
  const { preview, note, hover, toggle, setNote } = line
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  if (!row.markers.length) return null
  return (
    <span className="pokedex-markers">
      {row.markers.map((marker: Marker, index) => {
        const label = markerLabel(marker, language, t)
        if (marker.kind === 'forms') {
          return (
            <Link
              key="forms"
              to={pokemonPath(row.entry)}
              className="ui-version pokedex-marker"
              data-kind="forms"
              title={t('pokedex.formsTitle', { count: marker.count })}
            >
              {label}
            </Link>
          )
        }
        if (!('version' in marker)) {
          const kind = marker.kind
          return (
            <button
              key={kind}
              type="button"
              className="ui-version pokedex-marker"
              data-kind={kind}
              aria-pressed={note === kind}
              aria-label={t(`pokedex.sameStats.${kind}`)}
              title={t(`pokedex.sameStats.${kind}`)}
              onClick={() => setNote(note === kind ? null : kind)}
            >
              {label}
            </button>
          )
        }
        const version = marker.version
        const name = versionName(version, language)
        return (
          <button
            key={`${marker.kind}-${version.id}-${index}`}
            type="button"
            className="ui-version pokedex-marker"
            data-kind={marker.kind === 'regional' ? marker.region : marker.kind}
            aria-pressed={preview?.id === version.id}
            aria-label={t('pokedex.showVersion', { name })}
            onPointerEnter={(event) => {
              if (event.pointerType === 'mouse') hover(version)
            }}
            onPointerLeave={(event) => {
              if (event.pointerType === 'mouse') hover(null)
            }}
            onClick={() => toggle(version)}
          >
            {label}
          </button>
        )
      })}
    </span>
  )
}

interface Line {
  /** The version the line shows now. */
  shown: Version
  /** The version a marker is showing (hovered or tapped), if any. */
  preview: Version | null
  note: Note | null
  hover: (version: Version | null) => void
  toggle: (version: Version) => void
  setNote: (note: Note | null) => void
}

/** The state of one line: the version a marker shows and the open note. A tap sticks; a hover lasts while on it. */
function useLine(row: Row): Line {
  const [tapped, setTapped] = useState<Version | null>(null)
  const [hovered, setHovered] = useState<Version | null>(null)
  const [note, setNote] = useState<Note | null>(null)
  const preview = hovered ?? tapped
  return {
    shown: preview ?? row.shown,
    preview,
    note,
    hover: setHovered,
    toggle: (version) => {
      setHovered(null)
      setTapped(tapped?.id === version.id ? null : version)
    },
    setNote,
  }
}

function LineNotes({ row, shown, note }: { row: Row; shown: Version; note: Note | null }) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  return (
    <>
      {shown.id !== row.entry.main.id && (
        <span className="pokedex-showing">
          {t('pokedex.showing', { name: versionName(shown, language) })}
        </span>
      )}
      {!shown.released && <span className="pokedex-unreleased">{t('pokedex.notInGo')}</span>}
      {note && <span className="pokedex-note">{t(`pokedex.sameStats.${note}`)}</span>}
    </>
  )
}

function Picture({ shown, size }: { shown: Version; size: number }) {
  return <SpriteBox src={pictureSources(shown)} alt="" size={size} />
}

function PhoneLine({ row, strong }: { row: Row; strong: Stat | null }) {
  const language = useCurrentLanguage()
  const formats = useFormats()
  const line = useLine(row)
  const { shown } = line
  const stat = (key: 'attack' | 'defense' | 'stamina') => (
    <span data-strong={strong === key || undefined}>{formats.number(shown[key])}</span>
  )
  return (
    <li className="pokedex-line" data-dex={row.entry.dex}>
      <Picture shown={line.shown} size={64} />
      <div className="pokedex-line-main">
        <span className="pokedex-number num">#{String(row.entry.dex).padStart(3, '0')}</span>
        <Link className="pokedex-name pokedex-line-link" to={pokemonPath(row.entry, shown)}>
          {speciesName(row.entry, language)}
        </Link>
        <span className="pokedex-types">
          {shown.types.map((type) => (
            <TypeBadge key={type} type={type} />
          ))}
        </span>
        <Markers row={row} line={line} />
        <LineNotes row={row} shown={shown} note={line.note} />
      </div>
      <div className="pokedex-line-stats num">
        <span className="pokedex-cp" data-strong={strong === 'maxCp' || undefined}>
          {formats.number(shown.maxCp)}
        </span>
        <span className="pokedex-abc">
          {stat('attack')} · {stat('defense')} · {stat('stamina')}
        </span>
      </div>
    </li>
  )
}

function TableLine({ row, strong }: { row: Row; strong: Stat | null }) {
  const language = useCurrentLanguage()
  const formats = useFormats()
  const line = useLine(row)
  const { shown } = line
  return (
    <tr className="pokedex-row" data-dex={row.entry.dex}>
      <td className="pokedex-number num">#{String(row.entry.dex).padStart(3, '0')}</td>
      <td>
        <span className="pokedex-who">
          <Picture shown={line.shown} size={44} />
          <Link className="pokedex-name pokedex-line-link" to={pokemonPath(row.entry, shown)}>
            {speciesName(row.entry, language)}
          </Link>
        </span>
      </td>
      <td>
        <span className="pokedex-types">
          {shown.types.map((type) => (
            <TypeBadge key={type} type={type} />
          ))}
        </span>
      </td>
      <td>
        <Markers row={row} line={line} />
        <span className="pokedex-line-notes">
          <LineNotes row={row} shown={shown} note={line.note} />
        </span>
      </td>
      {stats.map((stat) => (
        <td key={stat} className="pokedex-stat num" data-strong={strong === stat || undefined}>
          {formats.number(shown[stat])}
        </td>
      ))}
    </tr>
  )
}

function SortHeader({
  column,
  filters,
  onSort,
  className,
}: {
  column: SortKey
  filters: Filters
  onSort: (filters: Filters) => void
  className?: string
}) {
  const { t } = useTranslation()
  const active = filters.sort === column
  return (
    <th
      scope="col"
      className={className}
      aria-sort={active ? (filters.order === 'asc' ? 'ascending' : 'descending') : undefined}
    >
      <button type="button" className="sort-header" onClick={() => onSort(sortBy(filters, column))}>
        {column === 'number'
          ? '#'
          : column === 'name'
            ? t('pokedex.columns.pokemon')
            : t(`pokedex.columns.${column}`)}
        {active && <span aria-hidden="true">{filters.order === 'asc' ? ' ↑' : ' ↓'}</span>}
      </button>
    </th>
  )
}

/**
 * The lines: cards on the phone, a table on the desktop whose column headers sort. `strong` is the stat a filter or
 * the search picked the versions by: it's coloured.
 */
export function PokedexList({
  rows,
  filters,
  strong,
  onSort,
}: {
  rows: Row[]
  filters: Filters
  strong: Stat | null
  onSort: (filters: Filters) => void
}) {
  const { t } = useTranslation()
  const desktop = useIsDesktop()
  if (!desktop) {
    return (
      <ul className="pokedex-lines ui-card" aria-label={t('titles.pokedex')}>
        {rows.map((row) => (
          <PhoneLine key={row.entry.dex} row={row} strong={strong} />
        ))}
      </ul>
    )
  }
  return (
    <div className="pokedex-table-card ui-card">
      <table className="pokedex-table">
        <thead>
          <tr>
            <SortHeader
              column="number"
              filters={filters}
              onSort={onSort}
              className="pokedex-number"
            />
            <SortHeader column="name" filters={filters} onSort={onSort} />
            <th scope="col">{t('pokedex.columns.types')}</th>
            <th scope="col">{t('pokedex.columns.versions')}</th>
            {stats.map((stat) => (
              <SortHeader
                key={stat}
                column={stat}
                filters={filters}
                onSort={onSort}
                className="pokedex-stat"
              />
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <TableLine key={row.entry.dex} row={row} strong={strong} />
          ))}
        </tbody>
      </table>
    </div>
  )
}
