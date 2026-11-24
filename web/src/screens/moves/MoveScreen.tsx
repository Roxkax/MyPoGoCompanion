import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useLocation, useParams } from 'react-router'
import { $api, type Schemas } from '../../api/client'
import { LoadError } from '../../components/LoadError'
import { useCurrentLanguage, useFormats } from '../../i18n/languageContext'
import { locales } from '../../i18n/languages'
import { DetailScreen } from '../../layout/Screen'
import { useIsDesktop } from '../../layout/useMediaQuery'
import { MoveEffect } from '../../moves/MoveEffect'
import { moveName, type Move } from '../../moves/view'
import { isPokemonType } from '../../ui/pokemonTypes'
import { SpriteBox } from '../../ui/SpriteBox'
import { pictureSources } from '../../ui/sprites'
import { TypeBadge } from '../../ui/TypeBadge'
import { VersionMarker } from '../../ui/VersionMarker'

const groups = ['EliteTm', 'Legacy', 'Normal'] as const
type Learner = Schemas['MoveLearner']

export function MoveScreen() {
  const { move = '' } = useParams()
  const { t } = useTranslation()
  const state = useLocation().state as { movesSearch?: unknown } | null
  const search = typeof state?.movesSearch === 'string' ? state.movesSearch : ''
  const back = {
    to: search ? `/pokedex/moves?${search}` : '/pokedex/moves',
    label: 'moves',
  } as const
  const query = $api.useQuery(
    'get',
    '/moves/{id}',
    { params: { path: { id: move } } },
    {
      staleTime: 60 * 60 * 1000,
      retry: false,
    },
  )
  return (
    <DetailScreen back={back}>
      {query.data ? (
        <MoveDetails key={query.data.move.id} page={query.data} />
      ) : query.error?.status === 404 ? (
        <section className="ui-card ui-card-padded">
          <h1>{t('movePage.notFound')}</h1>
        </section>
      ) : query.isPending ? (
        <p className="pokedex-loading" role="status">
          {t('movePage.loading')}
        </p>
      ) : (
        <LoadError error={query.error} onRetry={() => void query.refetch()} />
      )}
    </DetailScreen>
  )
}

function MoveDetails({ page }: { page: Schemas['MovePageResponse'] }) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const { move } = page
  return (
    <div className="move-page">
      <header className="move-page-heading">
        <h1>{moveName(move, language)}</h1>
        <div>
          {isPokemonType(move.type) ? <TypeBadge type={move.type} /> : move.type}{' '}
          {t(move.kind === 'Fast' ? 'movePage.fast' : 'movePage.charged')}
        </div>
      </header>
      <div className="move-stat-cards">
        <MoveStats move={move} raids />
        <MoveStats move={move} raids={false} />
      </div>
      <h2>{t('movePage.learners')}</h2>
      {!!page.events.length && (
        <section className="ui-card ui-card-padded move-events" aria-labelledby="move-events">
          <h3 id="move-events">{t('movePage.events')}</h3>
          {page.events.map((event) => (
            <EventNote key={`${event.eventId}:${event.formId}`} event={event} />
          ))}
        </section>
      )}
      {groups.map((group) => (
        <LearnerGroup
          key={group}
          group={group}
          rows={page.learners.filter((row) => row.availability === group)}
          fast={move.kind === 'Fast'}
        />
      ))}
    </div>
  )
}

function MoveStats({ move, raids }: { move: Move; raids: boolean }) {
  const { t } = useTranslation()
  const formats = useFormats()
  const keys = raids
    ? (['raidPower', 'raidDurationMs', 'raidEnergy'] as const)
    : (['pvpPower', 'pvpEnergy', 'pvpTurns'] as const)
  const raidAvailable = move.raidDurationMs > 0
  const bars =
    raidAvailable && move.kind === 'Charged' && move.raidEnergy < 0
      ? Math.floor(100 / Math.abs(move.raidEnergy))
      : 0
  const heading = raids ? 'move-raids' : 'move-pvp'
  return (
    <section className="ui-card ui-card-padded move-stat-card" aria-labelledby={heading}>
      <h2 id={heading}>{t(raids ? 'movePage.raids' : 'moves.pvpLong')}</h2>
      <dl>
        {keys.map((key) => {
          const value = raids && !raidAvailable ? null : move[key]
          const display =
            value == null
              ? '—'
              : key === 'raidDurationMs'
                ? t('moves.seconds', {
                    value: formats.number(value / 1000, { maximumFractionDigits: 2 }),
                  })
                : key.endsWith('Energy')
                  ? `${value > 0 ? '+' : value < 0 ? '−' : ''}${formats.number(Math.abs(value))}`
                  : formats.number(value)
          return (
            <div key={key}>
              <dt>{t(`moves.columns.${key}`)}</dt>
              <dd>{display}</dd>
            </div>
          )
        })}
      </dl>
      {raids && bars > 0 && (
        <div className="move-bars">
          <span
            className="move-bars-graphic"
            aria-hidden="true"
            style={{ gridTemplateColumns: `repeat(${bars}, 1fr)` }}
          >
            {Array.from({ length: bars }, (_, i) => (
              <i key={i} />
            ))}
          </span>
          {t('movePage.bars', { count: bars })}
        </div>
      )}
      {!raids && (
        <div>
          {t('moves.effect')}: <MoveEffect move={move} />
        </div>
      )}
    </section>
  )
}

function LearnerGroup({
  group,
  rows,
  fast,
}: {
  group: (typeof groups)[number]
  rows: Learner[]
  fast: boolean
}) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const formats = useFormats()
  const desktop = useIsDesktop()
  const [expanded, setExpanded] = useState(false)
  const shown = desktop || expanded ? rows : rows.slice(0, 8)
  const id = `learners-${group}`
  return (
    <section className="ui-card move-learners" aria-labelledby={id}>
      <header>
        <h3 id={id}>{t(`movePage.${group}`)}</h3>
        <span>
          {rows.length ? `${formats.number(rows.length)} Pokémon` : t('movePage.none')}
          {group === 'Normal' && rows.length > 0 && (
            <> · {t(fast ? 'movePage.fastTm' : 'movePage.chargedTm')}</>
          )}
        </span>
      </header>
      {shown.length > 0 && (
        <ul id={`${id}-list`}>
          {shown.map((row) => (
            <li key={row.formId}>
              <Link
                to={`/pokedex/${row.dex}?version=${encodeURIComponent(row.formId)}`}
                className="move-learner"
              >
                <SpriteBox src={pictureSources(row)} size={40} alt="" />
                <div>
                  <strong>{(language === 'pt-PT' && row.namePt) || row.nameEn}</strong>
                  <div className="move-learner-types">
                    {row.types.map((type) => (
                      <span key={type}>
                        {isPokemonType(type) ? <TypeBadge type={type} /> : type}
                      </span>
                    ))}
                    {row.megaToo && <VersionMarker kind="mega" label={t('movePage.megaToo')} />}
                    {row.primalToo && (
                      <VersionMarker kind="primal" label={t('movePage.primalToo')} />
                    )}
                  </div>
                </div>
                <span className="move-attack">
                  <strong>{formats.number(row.attack)}</strong>
                  <small>{t('movePage.attack')}</small>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
      {!desktop && rows.length > 8 && (
        <button
          className="move-expand"
          type="button"
          aria-controls={`${id}-list`}
          aria-expanded={expanded}
          onClick={() => setExpanded(!expanded)}
        >
          {expanded ? t('movePage.showFewer') : t('movePage.showAll', { count: rows.length })}
        </button>
      )}
    </section>
  )
}

function EventNote({ event }: { event: Schemas['MoveEvent'] }) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const portuguese = language === 'pt-PT'
  const dateTime = new Intl.DateTimeFormat(locales[language], {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'UTC',
  })
  // The source provides a wall-clock value. Display it with its own zone label.
  const date = (value: string) => dateTime.format(new Date(`${value.replace(/Z$/, '')}Z`))
  const how = (portuguese && event.howPt) || event.howEn
  return (
    <div className="move-event">
      <strong>{(portuguese && event.namePt) || event.nameEn}</strong>
      <p>
        {t('movePage.dates')}: <time dateTime={event.startsAt}>{date(event.startsAt)}</time>
        {event.endsAt && (
          <>
            {' '}
            – <time dateTime={event.endsAt}>{date(event.endsAt)}</time>
          </>
        )}
        {' · '}
        {event.timeZone ?? t('movePage.localTime')}
      </p>
      <p>
        {(portuguese && event.pokemonNamePt) || event.pokemonNameEn}
        {how && <> — {how}</>}
      </p>
    </div>
  )
}
