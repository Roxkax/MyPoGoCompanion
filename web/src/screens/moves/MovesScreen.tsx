import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router'
import { $api } from '../../api/client'
import { Icon } from '../../components/icons'
import { LoadError } from '../../components/LoadError'
import { useCurrentLanguage, useFormats } from '../../i18n/languageContext'
import { pokedexSections } from '../../layout/nav'
import { TabScreen } from '../../layout/Screen'
import { useIsDesktop } from '../../layout/useMediaQuery'
import {
  filterMoves,
  moveName,
  noFilters,
  readFilters,
  setKind,
  setMode,
  sortBy,
  writeFilters,
  type Move,
  type MoveFilters,
  type MoveSort,
} from '../../moves/view'
import { Popover } from '../../ui/Popover'
import { isPokemonType, pokemonTypes } from '../../ui/pokemonTypes'
import { SegmentedSwitch } from '../../ui/SegmentedSwitch'
import { TypeBadge } from '../../ui/TypeBadge'

const raidColumns = ['raidPower', 'raidDurationMs', 'raidEnergy'] as const
const pvpColumns = ['pvpPower', 'pvpEnergy', 'pvpTurns'] as const

/** B3: one download, searched and sorted locally; the address preserves the current view. */
export function MovesScreen() {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const formats = useFormats()
  const desktop = useIsDesktop()
  const [params, setParams] = useSearchParams()
  const [filters, setFilters] = useAddressFilters(params)
  const query = $api.useQuery('get', '/moves', undefined, { staleTime: 60 * 60 * 1000 })
  const all = query.data?.moves
  const rows = useMemo(
    () => all && filterMoves(all, filters, language, (type) => t(`types.${type}`)),
    [all, filters, language, t],
  )
  const change = (next: MoveFilters) => {
    setFilters(next)
    void setParams(writeFilters(next), { replace: true })
  }
  const kindName = t(filters.kind === 'fast' ? 'moves.fastMoves' : 'moves.chargedMoves')
  const count = rows
    ? t(filters.kind === 'fast' ? 'moves.fastCount' : 'moves.chargedCount', {
        count: rows.length,
        formatted: formats.number(rows.length),
      })
    : null
  const sortColumns: MoveSort[] = [
    'name',
    'type',
    ...(filters.mode === 'raids' ? raidColumns : pvpColumns),
  ].filter((column) => column !== 'pvpTurns' || filters.kind === 'fast') as MoveSort[]
  const search = (
    <div className="pokedex-search moves-search" role="search">
      <Icon name="search" size={19} />
      <input
        type="search"
        value={filters.q}
        placeholder={t('moves.search')}
        aria-label={t('moves.search')}
        onChange={(event) => change({ ...filters, q: event.target.value })}
        autoComplete="off"
        autoCapitalize="off"
        autoCorrect="off"
        spellCheck={false}
      />
      {filters.q && (
        <button
          type="button"
          className="pokedex-search-button"
          aria-label={t('moves.clearSearch')}
          onClick={() => change({ ...filters, q: '' })}
        >
          <Icon name="close" size={16} />
        </button>
      )}
    </div>
  )
  const kindSwitch = (
    <SegmentedSwitch
      label={t('moves.kind')}
      value={filters.kind}
      onChange={(kind) => change(setKind(filters, kind))}
      options={[
        {
          value: 'fast',
          label: t('moves.fast'),
          count: all?.filter((m) => m.kind === 'Fast').length,
        },
        {
          value: 'charged',
          label: t('moves.charged'),
          count: all?.filter((m) => m.kind === 'Charged').length,
        },
      ]}
    />
  )
  const typeFilter = (
    <Popover
      label={`${t('moves.type')}${filters.type && desktop ? `: ${t(`types.${filters.type}`)}` : ''} ▾`}
      title={t('moves.type')}
      active={!!filters.type}
    >
      <button
        type="button"
        className="ui-chip"
        aria-pressed={!filters.type}
        onClick={() => change({ ...filters, type: null })}
      >
        {t('moves.allTypes')}
      </button>
      <div className="type-grid">
        {pokemonTypes.map((type) => (
          <button
            key={type}
            type="button"
            className="type-choice"
            data-type={type}
            aria-pressed={filters.type === type}
            onClick={() => change({ ...filters, type: filters.type === type ? null : type })}
          >
            {t(`types.${type}`)}
          </button>
        ))}
      </div>
    </Popover>
  )
  const tools = (
    <div className="moves-tools">
      {desktop && search}
      <div className="moves-kind">{kindSwitch}</div>
      <div className="moves-filters">
        {typeFilter}
        {filters.type && !desktop && (
          <button
            className="ui-chip"
            type="button"
            data-active
            aria-label={t('moves.removeType', { type: t(`types.${filters.type}`) })}
            onClick={() => change({ ...filters, type: null })}
          >
            {t(`types.${filters.type}`)} <span aria-hidden="true">×</span>
          </button>
        )}
        {!desktop && (
          <span className="moves-sort">
            <Popover
              label={t('moves.sort', {
                column: t(`moves.sortColumns.${filters.sort}`),
                arrow: filters.order === 'asc' ? '↑' : '↓',
              })}
              title={t('moves.sortTitle')}
            >
              <ul className="sort-options">
                {sortColumns.map((column) => (
                  <li key={column}>
                    <button
                      type="button"
                      className="sort-option"
                      aria-pressed={filters.sort === column}
                      onClick={() => change(sortBy(filters, column))}
                    >
                      {t(`moves.sortColumns.${column}`)}
                      {filters.sort === column && (
                        <span aria-hidden="true">{filters.order === 'asc' ? '↑' : '↓'}</span>
                      )}
                    </button>
                  </li>
                ))}
              </ul>
            </Popover>
          </span>
        )}
      </div>
      {desktop && (
        <p className="moves-count" aria-live="polite">
          {count}
        </p>
      )}
    </div>
  )

  return (
    <TabScreen
      title="pokedex"
      desktopTitle="moves"
      sections={{ label: 'pokedex', links: pokedexSections }}
      subtitle={
        desktop && all
          ? t('moves.total', { count: all.length, formatted: formats.number(all.length) })
          : undefined
      }
      toolbar={
        desktop ? undefined : (
          <>
            {search}
            {tools}
          </>
        )
      }
    >
      <div className="moves">
        {desktop && tools}
        {!desktop && (
          <div className="moves-mode">
            <p className="moves-count" aria-live="polite">
              {count}
            </p>
            <SegmentedSwitch
              label={t('moves.battleStats')}
              value={filters.mode}
              onChange={(mode) => change(setMode(filters, mode))}
              options={[
                { value: 'raids', label: t('moves.raids') },
                { value: 'pvp', label: t('moves.pvp') },
              ]}
            />
          </div>
        )}
        {query.isPending ? (
          <p className="pokedex-loading" role="status">
            {t('moves.loading')}
          </p>
        ) : !rows ? (
          <LoadError error={query.error} onRetry={() => void query.refetch()} />
        ) : rows.length === 0 ? (
          <section className="pokedex-empty ui-card ui-card-padded" aria-live="polite">
            <h2>{t('moves.emptyTitle')}</h2>
            <p>{t('moves.emptyText')}</p>
            <button
              type="button"
              className="button"
              onClick={() => change({ ...noFilters, kind: filters.kind, mode: filters.mode })}
            >
              {t('moves.clearFilters')}
            </button>
          </section>
        ) : (
          <MoveTable
            rows={rows}
            label={kindName}
            desktop={desktop}
            filters={filters}
            onChange={change}
          />
        )}
        {rows && rows.length > 0 && <p className="pokedex-footnote">{t('moves.note')}</p>}
      </div>
    </TabScreen>
  )
}

/** Keep typing responsive while React Router updates its location in the background. */
function useAddressFilters(params: URLSearchParams): [MoveFilters, (filters: MoveFilters) => void] {
  const address = params.toString()
  const [current, setCurrent] = useState(() => ({
    address,
    filters: readFilters(params),
    written: [] as string[],
  }))
  if (current.address !== address) {
    const at = current.written.indexOf(address)
    setCurrent(
      at >= 0
        ? { address, filters: current.filters, written: current.written.slice(at + 1) }
        : { address, filters: readFilters(params), written: [] },
    )
  }
  return [
    current.filters,
    (filters) =>
      setCurrent((state) => ({
        ...state,
        filters,
        written: [...state.written, writeFilters(filters).toString()],
      })),
  ]
}

function MoveTable({
  rows,
  label,
  desktop,
  filters,
  onChange,
}: {
  rows: Move[]
  label: string
  desktop: boolean
  filters: MoveFilters
  onChange: (filters: MoveFilters) => void
}) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const formats = useFormats()
  const fast = filters.kind === 'fast'
  const raid = desktop || filters.mode === 'raids'
  const pvp = desktop || filters.mode === 'pvp'
  const header = (column: MoveSort, text: string, separator = false) => (
    <th
      key={column}
      scope="col"
      className={separator ? 'moves-separator' : undefined}
      aria-sort={
        filters.sort === column ? (filters.order === 'asc' ? 'ascending' : 'descending') : undefined
      }
    >
      <button
        type="button"
        aria-label={t(`moves.sortColumns.${column}`)}
        onClick={() => onChange(sortBy(filters, column))}
      >
        {text}
        {filters.sort === column && (
          <span aria-hidden="true"> {filters.order === 'asc' ? '↑' : '↓'}</span>
        )}
      </button>
    </th>
  )
  const number = (move: Move, column: Exclude<MoveSort, 'name' | 'type'>, separator = false) => {
    const unavailable = column.startsWith('raid') && move.raidDurationMs <= 0
    const value = unavailable ? null : move[column]
    const text =
      value == null
        ? '—'
        : column === 'raidDurationMs'
          ? t('moves.seconds', {
              value: formats.number(value / 1000, { maximumFractionDigits: 2 }),
            })
          : column.endsWith('Energy')
            ? `${value > 0 ? '+' : value < 0 ? '−' : ''}${formats.number(Math.abs(value))}`
            : formats.number(value)
    return (
      <td
        key={column}
        data-stat={column}
        className={separator ? 'moves-separator' : undefined}
        title={value == null ? t('moves.unavailable') : undefined}
      >
        {text}
      </td>
    )
  }
  return (
    <div className="ui-card moves-table-wrap">
      <table
        className="moves-table"
        aria-label={label}
        data-mode={filters.mode}
        data-fast={fast || undefined}
      >
        <thead>
          {desktop && (
            <tr className="moves-groups">
              <th colSpan={2} />
              <th scope="colgroup" colSpan={3} className="moves-separator">
                {t('moves.raids')}
              </th>
              <th scope="colgroup" colSpan={fast ? 4 : 3} className="moves-separator">
                {t('moves.pvpLong')}
              </th>
            </tr>
          )}
          <tr>
            {header('name', t('moves.move'))}
            {desktop && header('type', t('moves.type'))}
            {raid &&
              raidColumns.map((column, index) =>
                header(column, t(`moves.columns.${column}`), desktop && index === 0),
              )}
            {pvp &&
              pvpColumns
                .filter((column) => fast || column !== 'pvpTurns')
                .map((column, index) =>
                  header(column, t(`moves.columns.${column}`), desktop && index === 0),
                )}
            {pvp && (!fast || desktop) && <th scope="col">{t('moves.effect')}</th>}
          </tr>
        </thead>
        <tbody>
          {rows.map((move) => (
            <tr key={move.id} data-move={move.id}>
              <th scope="row">
                <Link
                  className="moves-name"
                  to={`/pokedex/moves/${encodeURIComponent(move.id.toLowerCase().replaceAll('_', '-'))}`}
                >
                  <span
                    className="moves-dot"
                    style={{ background: `var(--t-${move.type})` }}
                    aria-hidden="true"
                  />
                  <span>{moveName(move, language)}</span>
                  {!desktop && isPokemonType(move.type) && (
                    <span className="moves-type-name">{t(`types.${move.type}`)}</span>
                  )}
                </Link>
              </th>
              {desktop && (
                <td className="moves-type">
                  {isPokemonType(move.type) ? <TypeBadge type={move.type} /> : move.type}
                </td>
              )}
              {raid &&
                raidColumns.map((column, index) => number(move, column, desktop && index === 0))}
              {pvp &&
                pvpColumns
                  .filter((column) => fast || column !== 'pvpTurns')
                  .map((column, index) => number(move, column, desktop && index === 0))}
              {pvp && (!fast || desktop) && (
                <td className="moves-effect">
                  <MoveEffect move={move} />
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/** Show all simultaneous stat changes, including their probability; no effect is inferred from a move's name. */
function MoveEffect({ move }: { move: Move }) {
  const { t } = useTranslation()
  const formats = useFormats()
  const changes = [
    ['self', 'attack', move.pvpSelfAttack],
    ['self', 'defense', move.pvpSelfDefense],
    ['foe', 'attack', move.pvpOpponentAttack],
    ['foe', 'defense', move.pvpOpponentDefense],
  ] as const
  const active = changes.filter(([, , value]) => value !== 0)
  if (!active.length || move.pvpBuffChance === 0) return <span className="moves-no-effect">—</span>
  return (
    <span className="moves-effects">
      {active.map(([who, stat, value]) => (
        <span key={`${who}-${stat}`} data-target={who}>
          {t(`moves.effects.${who}`)} {t(`moves.effects.${stat}`)} {value > 0 ? '+' : '−'}
          {formats.number(Math.abs(value))}
        </span>
      ))}
      <small>
        {move.pvpBuffChance == null
          ? t('moves.effects.unknownChance')
          : t('moves.effects.chance', {
              value: formats.number(move.pvpBuffChance, {
                style: 'percent',
                maximumFractionDigits: 1,
              }),
            })}
      </small>
    </span>
  )
}
