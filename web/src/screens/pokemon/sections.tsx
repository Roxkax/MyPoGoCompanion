import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { useCurrentLanguage, useFormats } from '../../i18n/languageContext'
import { normalize } from '../../pokedex/normalize'
import {
  checkSpread,
  clampIv,
  evolutionChain,
  evolutionsAfter,
  keeperRanges,
  leagueCaps,
  leaguePath,
  leagues,
  leagueSpreads,
  matchupsOf,
  movepoolOf,
  multiplierText,
  pvpMovesets,
  splitAppearances,
  typesOf,
  usageShares,
  wallClock,
  type League,
  type MoveRow,
  type PageAppearance,
  type PageEvolution,
  type PageMove,
  type PageVersion,
  type PokemonPage,
  type Spread,
} from '../../pokemon/page'
import { Card } from '../../ui/Card'
import type { PokemonType } from '../../ui/pokemonTypes'
import { SegmentedSwitch } from '../../ui/SegmentedSwitch'
import { pictureSources, spriteUrl } from '../../ui/sprites'
import { SpriteBox } from '../../ui/SpriteBox'
import { formName, memberName, moveName, speciesNameOf } from './names'

export interface SectionProps {
  page: PokemonPage
  version: PageVersion
}

/** Stat bars reach their end at this value (Mega Mewtwo X's Attack is 412; most are far below). */
const statScale = 350

function Section({
  id,
  title,
  children,
}: {
  id: string
  title: string
  children: React.ReactNode
}) {
  return (
    <section className="pk-section" aria-labelledby={`pk-${id}`}>
      <h2 className="pk-section-title" id={`pk-${id}`}>
        {title}
      </h2>
      {children}
    </section>
  )
}

function useMoves(page: PokemonPage): ReadonlyMap<string, PageMove> {
  return useMemo(() => new Map(page.moves.map((move) => [move.id, move])), [page.moves])
}

// ---------- Overview ----------

export function StatsSection({ version }: SectionProps) {
  const { t } = useTranslation()
  const formats = useFormats()
  const form = version.form
  const stats = [
    ['attack', form.attack],
    ['defense', form.defense],
    ['stamina', form.stamina],
  ] as const
  return (
    <Section id="stats" title={t('pokemon.stats.title')}>
      <Card className="pk-stats">
        <dl className="pk-bars">
          {stats.map(([key, value]) => (
            <div key={key} className="pk-bar-row">
              <dt>{t(`pokemon.stats.${key}`)}</dt>
              <dd>
                <span className="pk-bar" aria-hidden="true">
                  <span style={{ width: `${Math.min(100, (value / statScale) * 100)}%` }} />
                </span>
                <span className="pk-bar-value">{formats.number(value)}</span>
              </dd>
            </div>
          ))}
        </dl>
        <dl className="pk-maxcp">
          {form.maxCp.map((row) => (
            <div key={row.level}>
              <dt>{t('pokemon.stats.maxCp', { level: row.level })}</dt>
              <dd>{formats.number(row.cp)}</dd>
            </div>
          ))}
        </dl>
        {version.shadow && (
          <p className="pk-note">{t('pokemon.sameStatsShadow', { name: t('versions.shadow') })}</p>
        )}
      </Card>
    </Section>
  )
}

export function RankingsSection({ page, version }: SectionProps) {
  const { t } = useTranslation()
  const formats = useFormats()
  const form = version.form
  const megas = page.forms.filter((f) => f.kind === 'Mega' || f.kind === 'Primal')
  const rank = (value: number | undefined) =>
    value === undefined
      ? t('pokemon.rankings.notRanked')
      : t('pokemon.rankings.rank', { rank: formats.number(value) })

  return (
    <Section id="rankings" title={t('pokemon.rankings.title')}>
      <Card padded={false}>
        <ul className="card-rows pk-links">
          {leagues.map((league) => {
            const own = form.pvp.find((r) => r.league === league && r.isShadow === version.shadow)
            const shadow = !version.shadow
              ? form.pvp.find((r) => r.league === league && r.isShadow)
              : undefined
            return (
              <li key={league}>
                <Link to={leaguePath(league)}>
                  <span className="pk-link-text">
                    <span>{t(`pokemon.leagues.${league}`)}</span>
                    {shadow && (
                      <small>
                        {t('pokemon.rankings.shadowRank', { rank: formats.number(shadow.rank) })}
                      </small>
                    )}
                  </span>
                  <span className="pk-link-value">{rank(own?.rank)}</span>
                </Link>
              </li>
            )
          })}
          {typesOf(form).map((type) => {
            const own = form.raidRanks.find((r) => r.type === type && r.isShadow === version.shadow)
            const shadow = !version.shadow
              ? form.raidRanks.find((r) => r.type === type && r.isShadow)
              : undefined
            const mega =
              form.kind === 'Mega' || form.kind === 'Primal'
                ? undefined
                : megas
                    .flatMap((m) => m.raidRanks.filter((r) => r.type === type && !r.isShadow))
                    .sort((a, b) => a.rank - b.rank)[0]
            const extra = [
              shadow && t('pokemon.rankings.shadowRank', { rank: formats.number(shadow.rank) }),
              mega && t('pokemon.rankings.megaRank', { rank: formats.number(mega.rank) }),
            ].filter(Boolean)
            return (
              <li key={type}>
                <Link to={`/battle/tiers/raids/${type}`}>
                  <span className="pk-link-text">
                    <span>{t('pokemon.rankings.raids', { type: t(`types.${type}`) })}</span>
                    {extra.length > 0 && <small>{extra.join(' · ')}</small>}
                  </span>
                  <span className="pk-link-value">{rank(own?.rank)}</span>
                </Link>
              </li>
            )
          })}
        </ul>
      </Card>
    </Section>
  )
}

export function MatchupsSection({ version }: SectionProps) {
  const { t } = useTranslation()
  const { weak, resists } = matchupsOf(version.form)
  const chips = (list: typeof weak) =>
    list.length === 0 ? (
      <p className="pk-muted">{t('pokemon.matchups.none')}</p>
    ) : (
      <div className="pk-matchups">
        {list.map((m) => (
          <span key={m.type} className="ui-type" data-type={m.type}>
            {t(`types.${m.type as PokemonType}`)} {multiplierText(m.multiplier)}
          </span>
        ))}
      </div>
    )
  return (
    <Section id="matchups" title={t('pokemon.matchups.title')}>
      <Card>
        <h3 className="pk-label">{t('pokemon.matchups.weak')}</h3>
        {chips(weak)}
        <h3 className="pk-label">{t('pokemon.matchups.resists')}</h3>
        {chips(resists)}
        <Link className="pk-more" to={`/pokedex/types?types=${typesOf(version.form).join(',')}`}>
          {t('pokemon.matchups.openChart')} ›
        </Link>
      </Card>
    </Section>
  )
}

// ---------- Moves ----------

function leagueNames(list: League[], t: ReturnType<typeof useTranslation>['t']): string {
  const names = list.map((league) => t(`pokemon.leagues.${league}`))
  if (names.length === 1) return names[0] ?? ''
  const last = names.pop() ?? ''
  return t('pokemon.leagueAnd', { first: names.join(', '), second: last })
}

export function MovesSection({ page, version }: SectionProps) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const formats = useFormats()
  const moves = useMoves(page)
  const form = version.form
  const name = (id: string | null | undefined) => (id ? moveName(moves.get(id), id, language) : '')
  const best = [
    ...typesOf(form).flatMap((type) => {
      const ranked = form.raidRanks.find((r) => r.type === type && r.isShadow === version.shadow)
      return ranked
        ? [
            {
              key: `raid-${type}`,
              label: t('pokemon.moves.bestRaid', { type: t(`types.${type}`) }),
              text: t('pokemon.moves.plus', {
                fast: name(ranked.fastMoveId),
                charged: name(ranked.chargedMoveId),
              }),
            },
          ]
        : []
    }),
    ...pvpMovesets(form, version.shadow).map(({ leagues: list, ranking }) => ({
      key: `pvp-${list.join('-')}`,
      label: leagueNames(list, t),
      text: t('pokemon.moves.plus', {
        fast: name(ranking.fastMoveId),
        charged: [ranking.chargedMove1Id, ranking.chargedMove2Id]
          .filter(Boolean)
          .map(name)
          .join(', '),
      }),
    })),
  ]
  const pool = movepoolOf(form, moves)

  const row = ({ move, availability }: MoveRow) => {
    const raid =
      move.raidDurationMs > 0
        ? `${formats.number(move.raidPower)} · ${t('pokemon.moves.seconds', { value: formats.number(move.raidDurationMs / 1000, { minimumFractionDigits: 1, maximumFractionDigits: 1 }) })} · ${signed(move.raidEnergy, formats)}`
        : t('pokemon.moves.noRaid')
    const pvp =
      move.kind === 'Fast'
        ? `${formats.number(move.pvpPower)} · ${signed(move.pvpEnergy, formats)} (${t('pokemon.moves.turns', { count: move.pvpTurns ?? 1 })})`
        : `${formats.number(move.pvpPower)} · ${signed(move.pvpEnergy, formats)}`
    return (
      <li key={move.id} className="pk-move">
        <span className="pk-move-name">
          <span
            className="pk-dot"
            style={{ background: `var(--t-${move.type})` }}
            aria-hidden="true"
          />
          {moveName(move, move.id, language)}
          {availability !== 'Normal' && (
            <span className="pk-flag" data-flag={availability}>
              {t(availability === 'EliteTm' ? 'pokemon.moves.eliteTm' : 'pokemon.moves.legacy')}
            </span>
          )}
        </span>
        <span className="pk-move-numbers">
          <span>
            <b>{t('pokemon.moves.raids')}</b> {raid}
          </span>
          <span>
            <b>{t('pokemon.moves.pvp')}</b> {pvp}
          </span>
        </span>
      </li>
    )
  }

  return (
    <Section id="moves" title={t('pokemon.moves.title')}>
      {best.length > 0 && (
        <Card padded={false}>
          <ul className="card-rows pk-best">
            {best.map((item) => (
              <li key={item.key}>
                <small>{item.label}</small>
                <span>{item.text}</span>
              </li>
            ))}
          </ul>
        </Card>
      )}
      <Card padded={false} className="pk-movepool">
        {pool.fast.length + pool.charged.length === 0 ? (
          <p className="pk-muted pk-pad">{t('pokemon.moves.none')}</p>
        ) : (
          <>
            <h3 className="pk-group">{t('pokemon.moves.fast')}</h3>
            <ul className="pk-move-list">{pool.fast.map(row)}</ul>
            <h3 className="pk-group">{t('pokemon.moves.charged')}</h3>
            <ul className="pk-move-list">{pool.charged.map(row)}</ul>
          </>
        )}
      </Card>
    </Section>
  )
}

function signed(value: number, formats: ReturnType<typeof useFormats>): string {
  return value > 0 ? `+${formats.number(value)}` : value < 0 ? `−${formats.number(-value)}` : '0'
}

const shownMovesets = 8

export function RaidMovesetsSection({ page, version }: SectionProps) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const formats = useFormats()
  const moves = useMoves(page)
  const [all, setAll] = useState(false)
  const list = version.form.raidMovesets.filter((m) => m.isShadow === version.shadow)
  const best = list[0]?.score ?? 0
  const shown = all ? list : list.slice(0, shownMovesets)
  return (
    <Section id="raid-movesets" title={t('pokemon.raidMovesets.title')}>
      <Card padded={false}>
        {list.length === 0 ? (
          <p className="pk-muted pk-pad">{t('pokemon.raidMovesets.none')}</p>
        ) : (
          <table className="pk-table">
            <thead>
              <tr>
                <th scope="col">#</th>
                <th scope="col">{t('pokemon.moves.move')}</th>
                <th scope="col">{t('pokemon.raidMovesets.dps')}</th>
                <th scope="col">{t('pokemon.raidMovesets.tdo')}</th>
                <th scope="col">{t('pokemon.raidMovesets.ofBest')}</th>
              </tr>
            </thead>
            <tbody>
              {shown.map((m) => (
                <tr key={`${m.fastMoveId}-${m.chargedMoveId}`}>
                  <td>{formats.number(m.rank)}</td>
                  <th scope="row">
                    {moveName(moves.get(m.fastMoveId), m.fastMoveId, language)} +{' '}
                    {moveName(moves.get(m.chargedMoveId), m.chargedMoveId, language)}
                  </th>
                  <td>
                    {formats.number(m.dps, { maximumFractionDigits: 1, minimumFractionDigits: 1 })}
                  </td>
                  <td>{formats.number(Math.round(m.tdo))}</td>
                  <td>{formats.percent(best > 0 ? m.score / best : 0)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {list.length > shownMovesets && (
          <button type="button" className="pk-more pk-pad" onClick={() => setAll(!all)}>
            {all
              ? t('pokemon.raidMovesets.showFewer')
              : t('pokemon.raidMovesets.showAll', { count: list.length })}
          </button>
        )}
      </Card>
    </Section>
  )
}

export function PvpUsageSection({ page, version }: SectionProps) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const formats = useFormats()
  const moves = useMoves(page)
  const ranked = leagues.filter((league) =>
    version.form.pvp.some((r) => r.league === league && r.isShadow === version.shadow),
  )
  const [chosen, setChosen] = useState<League | null>(null)
  const league = chosen && ranked.includes(chosen) ? chosen : (ranked[0] ?? 'Great')
  const ranking = version.form.pvp.find((r) => r.league === league && r.isShadow === version.shadow)
  const shares = ranking ? usageShares(ranking.usage, moves) : null
  const name = (id: string | null | undefined) => (id ? moveName(moves.get(id), id, language) : '')

  const list = (rows: { move: PageMove; share: number }[]) => (
    <ul className="pk-usage">
      {rows.map(({ move, share }) => (
        <li key={move.id}>
          <span className="pk-move-name">
            <span
              className="pk-dot"
              style={{ background: `var(--t-${move.type})` }}
              aria-hidden="true"
            />
            {moveName(move, move.id, language)}
          </span>
          <span className="pk-bar" aria-hidden="true">
            <span style={{ width: `${share * 100}%` }} />
          </span>
          <span className="pk-bar-value">{formats.percent(share)}</span>
        </li>
      ))}
    </ul>
  )

  return (
    <Section id="usage" title={t('pokemon.usage.title')}>
      <Card>
        <p className="pk-muted">{t('pokemon.usage.text')}</p>
        {ranked.length > 1 && (
          <SegmentedSwitch
            label={t('pokemon.usage.leagueLabel')}
            options={ranked.map((value) => ({ value, label: t(`pokemon.leagues.${value}`) }))}
            value={league}
            onChange={setChosen}
          />
        )}
        {!ranking || !shares ? (
          <p className="pk-muted">{t('pokemon.usage.none')}</p>
        ) : (
          <>
            <p className="pk-recommended">
              {ranked.length === 1 && <b>{t(`pokemon.leagues.${league}`)} · </b>}
              {t('pokemon.usage.recommended', {
                moveset: t('pokemon.moves.plus', {
                  fast: name(ranking.fastMoveId),
                  charged: [ranking.chargedMove1Id, ranking.chargedMove2Id]
                    .filter(Boolean)
                    .map(name)
                    .join(', '),
                }),
              })}
            </p>
            <h3 className="pk-label">{t('pokemon.moves.fast')}</h3>
            {list(shares.fast)}
            <h3 className="pk-label">{t('pokemon.moves.charged')}</h3>
            {list(shares.charged)}
          </>
        )}
      </Card>
    </Section>
  )
}

// ---------- IVs ----------

export function BestIvsSection({ page, version }: SectionProps) {
  const { t } = useTranslation()
  const formats = useFormats()
  const form = version.form
  const mega = form.kind === 'Mega' || form.kind === 'Primal'
  return (
    <Section id="best-ivs" title={t('pokemon.ivs.title')}>
      <Card padded={false}>
        {form.bestIvs.length === 0 ? (
          <p className="pk-muted pk-pad">{t('pokemon.ivs.none')}</p>
        ) : (
          <ul className="card-rows pk-ivs">
            {form.bestIvs.map((best) => {
              const box = mega ? undefined : page.keeperBoxes.find((b) => b.league === best.league)
              const ranges = box ? keeperRanges(box) : null
              const range = ([low, high]: [number, number]) =>
                low === high ? `${low}` : `${low}–${high}`
              return (
                <li key={best.league}>
                  <span className="pk-ivs-head">
                    <span>{t(`pokemon.leagues.${best.league}`)}</span>
                    <span className="pk-ivs-spread">
                      {best.attack}/{best.defense}/{best.stamina}
                    </span>
                  </span>
                  <small>
                    {t('pokemon.ivs.levelCp', {
                      level: formats.number(best.level),
                      cp: formats.number(best.cp),
                    })}
                  </small>
                  {ranges && (
                    <small>
                      {t('pokemon.ivs.keep', {
                        attack: range(ranges[0]),
                        defense: range(ranges[1]),
                        stamina: range(ranges[2]),
                      })}
                    </small>
                  )}
                </li>
              )
            })}
          </ul>
        )}
      </Card>
    </Section>
  )
}

export function CheckIvsSection({ page, version }: SectionProps) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const formats = useFormats()
  const [text, setText] = useState({ attack: '15', defense: '15', stamina: '15' })
  const ivs: Spread = {
    attack: clampIv(text.attack),
    defense: clampIv(text.defense),
    stamina: clampIv(text.stamina),
  }
  // This version, then each evolution (its family member), as the spec asks.
  const targets = useMemo(() => {
    const later = evolutionsAfter(page, version.form.id)
      .map((id) => page.family.find((member) => member.formId === id))
      .filter((member) => member !== undefined)
    return [
      { key: version.form.id, name: null as string | null, stats: version.form },
      ...later.map((member) => ({
        key: member.formId,
        name: memberName(member, language),
        stats: member,
      })),
    ]
  }, [page, version.form, language])
  const spreads = useMemo(
    () =>
      targets.map((target) => ({
        ...target,
        leagues: leagues.map((league) => ({
          league,
          spreads: leagueSpreads(target.stats, leagueCaps[league], page.cpMultipliers),
        })),
      })),
    [targets, page.cpMultipliers],
  )

  const field = (key: keyof Spread, label: string) => (
    <label className="pk-iv-field">
      <span>{label}</span>
      <input
        type="number"
        inputMode="numeric"
        min={0}
        max={15}
        value={text[key]}
        onChange={(event) => setText({ ...text, [key]: event.target.value })}
        onBlur={() => setText({ ...text, [key]: String(ivs[key]) })}
      />
    </label>
  )

  return (
    <Section id="check-ivs" title={t('pokemon.ivs.checkTitle')}>
      <Card>
        <div className="pk-iv-fields">
          {field('attack', t('pokemon.ivs.attack'))}
          {field('defense', t('pokemon.ivs.defense'))}
          {field('stamina', t('pokemon.ivs.hp'))}
        </div>
        {spreads.map((target) => (
          <div key={target.key} className="pk-iv-target">
            {target.name && (
              <h3 className="pk-label">
                {t('pokemon.ivs.evolutionsTitle', { name: target.name })}
              </h3>
            )}
            <dl className="pk-iv-results" aria-live="polite">
              {target.leagues.map(({ league, spreads: all }) => {
                const check = checkSpread(all, ivs)
                return (
                  <div key={league} data-top={check.share >= 0.98 ? true : undefined}>
                    <dt>{t(`pokemon.leagues.${league}`)}</dt>
                    <dd>
                      <span className="pk-iv-rank">
                        {t('pokemon.ivs.result', {
                          rank: formats.number(check.rank),
                          share: formats.percent(check.share, 1),
                        })}
                      </span>
                      <small>
                        {t('pokemon.ivs.resultCp', {
                          level: formats.number(check.level),
                          cp: formats.number(check.cp),
                        })}
                      </small>
                    </dd>
                  </div>
                )
              })}
            </dl>
          </div>
        ))}
      </Card>
    </Section>
  )
}

const cheatRows = [
  ['research', 15],
  ['raids', 20],
  ['boostedRaids', 25],
  ['wild', 30],
  ['boostedWild', 35],
] as const

export function CheatSheetSection({ version }: SectionProps) {
  const { t } = useTranslation()
  const formats = useFormats()
  return (
    <Section id="cheat-sheet" title={t('pokemon.cheatSheet.title')}>
      <Card padded={false}>
        <ul className="card-rows pk-cheat">
          {cheatRows.map(([key, level]) => {
            const row = version.form.perfectCp.find((r) => r.level === level)
            return (
              <li key={key}>
                <span>
                  {t(`pokemon.cheatSheet.${key}`)}{' '}
                  <small>{t('pokemon.cheatSheet.level', { level })}</small>
                </span>
                <span className="pk-link-value">{row ? formats.number(row.cp) : '—'}</span>
              </li>
            )
          })}
        </ul>
      </Card>
    </Section>
  )
}

// ---------- Get it ----------

function stepText(
  step: PageEvolution,
  t: ReturnType<typeof useTranslation>['t'],
  formats: ReturnType<typeof useFormats>,
): string[] {
  const parts: string[] = []
  const candy = formats.number(step.candy)
  if (step.candyAfterTrade === 0) {
    parts.push(t('pokemon.evolution.candy', { count: candy }), t('pokemon.evolution.free'))
  } else if (step.candyAfterTrade != null) {
    parts.push(
      t('pokemon.evolution.candyTrade', {
        count: candy,
        trade: formats.number(step.candyAfterTrade),
      }),
    )
  } else if (step.candy > 0) {
    parts.push(t('pokemon.evolution.candy', { count: candy }))
  }
  if (step.itemId) parts.push(t('pokemon.evolution.item', { item: readableItem(step.itemId) }))
  if (step.buddyDistanceKm)
    parts.push(t('pokemon.evolution.buddy', { km: formats.number(step.buddyDistanceKm) }))
  if (step.timeOfDay === 'day' || step.timeOfDay === 'night')
    parts.push(t(`pokemon.evolution.${step.timeOfDay}`))
  if (step.gender === 'male' || step.gender === 'female')
    parts.push(t(`pokemon.evolution.${step.gender}`))
  if (step.lureId) parts.push(t('pokemon.evolution.lure', { lure: readableItem(step.lureId) }))
  return parts
}

/** `ITEM_SUN_STONE` → "Sun Stone", `ITEM_TROY_DISK_GLACIAL` → "Troy Disk Glacial". */
function readableItem(id: string): string {
  return id
    .replace(/^ITEM_/, '')
    .toLowerCase()
    .split('_')
    .filter(Boolean)
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(' ')
}

export function EvolutionSection({ page, version }: SectionProps) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const formats = useFormats()
  const stages = evolutionChain(page, version.form.id)
  return (
    <Section id="evolution" title={t('pokemon.evolution.title')}>
      <Card>
        {stages.length <= 1 ? (
          <p className="pk-muted">{t('pokemon.evolution.none')}</p>
        ) : (
          <ol className="pk-chain">
            {stages.map((stage, index) => (
              <li key={index} className="pk-stage">
                {stage.members.map(({ member, step }) => {
                  const slug = normalize(member.speciesNameEn).replace(/ /g, '-')
                  const to = member.isDefault
                    ? `/pokedex/${slug}`
                    : `/pokedex/${slug}?version=${member.formId}`
                  const task = step?.taskEn
                    ? ((language === 'pt-PT' ? step.taskPt : null) ?? step.taskEn)
                    : null
                  const extra = step ? [...stepText(step, t, formats), ...(task ? [task] : [])] : []
                  return (
                    <div key={member.formId} className="pk-stage-member">
                      {step && (
                        <span className="pk-step">
                          <span aria-hidden="true">→</span>
                          {extra.map((text) => (
                            <small key={text}>{text}</small>
                          ))}
                        </span>
                      )}
                      <Link
                        to={to}
                        className="pk-member"
                        aria-current={member.formId === version.form.id ? 'page' : undefined}
                      >
                        <SpriteBox
                          src={member.sprite ? [spriteUrl(member.sprite)] : null}
                          alt=""
                          size={56}
                        />
                        <span>{memberName(member, language)}</span>
                      </Link>
                    </div>
                  )
                })}
              </li>
            ))}
          </ol>
        )}
        <p className="pk-row">
          <span>{t('pokemon.evolution.buddyDistance')}</span>
          <b>{t('pokemon.evolution.km', { km: formats.number(version.form.buddyDistanceKm) })}</b>
        </p>
      </Card>
    </Section>
  )
}

function whereText(
  a: PageAppearance,
  t: ReturnType<typeof useTranslation>['t'],
  language: ReturnType<typeof useCurrentLanguage>,
): string {
  switch (a.kind) {
    case 'Raid': {
      const tier = a.raidTier
        ? t(`pokemon.getIt.tiers.${a.raidTier}`)
        : t('pokemon.getIt.kinds.Raid')
      return a.isShadow ? t('pokemon.getIt.shadowRaid', { tier }) : tier
    }
    case 'MaxBattle':
      return t(a.gigantamax ? 'pokemon.getIt.gmax' : 'pokemon.getIt.maxStars', {
        stars: a.maxStars ?? 1,
      })
    case 'Egg':
      return a.egg ? t(`pokemon.getIt.eggs.${a.egg}`) : t('pokemon.getIt.kinds.Egg')
    case 'Research':
      return t('pokemon.getIt.kinds.Research')
    case 'Event':
      return (
        ((language === 'pt-PT' ? a.eventNamePt : null) ?? a.eventNameEn) ||
        t('pokemon.getIt.kinds.Event')
      )
  }
}

export function GetItSection({ page }: SectionProps) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const formats = useFormats()
  const now = useMemo(() => new Date(), [])
  const split = useMemo(() => splitAppearances(page.appearances, now), [page.appearances, now])
  const formsById = new Map(page.forms.map((form) => [form.id, form]))

  const item = (a: PageAppearance, when: 'now' | 'coming' | 'history', index: number) => {
    const form = formsById.get(a.formId)
    const who = form ? formName(form, page, language) : speciesNameOf(page, language)
    const start = wallClock(a.startsAt)
    const end = wallClock(a.endsAt)
    const date =
      when === 'history'
        ? start && end
          ? t('pokemon.getIt.between', { from: formats.day(start), to: formats.day(end) })
          : end
            ? formats.day(end)
            : null
        : when === 'coming'
          ? start && t('pokemon.getIt.from', { when: formats.day(start) })
          : end && t('pokemon.getIt.until', { when: formats.day(end) })
    const task =
      a.kind === 'Research' ? ((language === 'pt-PT' ? a.taskPt : null) ?? a.taskEn) : null
    const event =
      a.kind !== 'Event' ? ((language === 'pt-PT' ? a.eventNamePt : null) ?? a.eventNameEn) : null
    const details = [task ? `${task} → ${who}` : who, event, date].filter(Boolean).join(' · ')
    return (
      <li key={`${when}-${index}`} className="pk-where">
        <span>{whereText(a, t, language)}</span>
        <span className="pk-where-details">
          {details}
          {a.shiny && (
            <span className="pk-shiny" title={t('pokemon.getIt.shiny')}>
              {' '}
              ✦
            </span>
          )}
        </span>
      </li>
    )
  }

  return (
    <Section id="get-it" title={t('pokemon.getIt.title')}>
      <Card padded={false}>
        <p className="card-rows-title">
          <span className="ui-pill" data-tone="ok">
            <span className="ui-pill-dot" aria-hidden="true" />
            {t('pokemon.getIt.rightNow')}
          </span>
        </p>
        {split.now.length + split.coming.length === 0 ? (
          <p className="pk-muted pk-pad">{t('pokemon.getIt.nothingNow')}</p>
        ) : (
          <ul className="card-rows pk-where-list">
            {split.now.map((a, i) => item(a, 'now', i))}
            {split.coming.map((a, i) => item(a, 'coming', i))}
          </ul>
        )}
      </Card>
      <Card padded={false}>
        <h3 className="card-rows-title pk-group-title">{t('pokemon.getIt.history')}</h3>
        {split.history.length === 0 ? (
          <p className="pk-muted pk-pad">{t('pokemon.getIt.noHistory')}</p>
        ) : (
          <ul className="card-rows pk-where-list">
            {split.history.map((a, i) => item(a, 'history', i))}
          </ul>
        )}
      </Card>
    </Section>
  )
}

// ---------- Versions ----------

export function VersionsSection({ page, version }: SectionProps) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const formats = useFormats()
  const tiles: { key: string; label: string; sources: string[]; className?: string }[] = []
  for (const form of page.forms) {
    const name = formName(form, page, language)
    tiles.push({ key: form.id, label: name, sources: pictureSources(form) })
    if (form.shiny === true) {
      tiles.push({
        key: `${form.id}-shiny`,
        label: `${t('pokemon.versionsTab.shiny')} ${form.isDefault ? '' : name}`.trim(),
        sources: pictureSources(form, { shiny: true }),
      })
    }
    if (form.shadow) {
      tiles.push({
        key: `${form.id}-shadow`,
        label: `${t('pokemon.versionsTab.shadow')}${form.isDefault ? '' : ` ${name}`}`,
        sources: pictureSources(form),
        className: 'pk-shadow',
      })
      tiles.push({
        key: `${form.id}-purified`,
        label: `${t('pokemon.versionsTab.purified')}${form.isDefault ? '' : ` ${name}`}`,
        sources: pictureSources(form),
      })
    }
    if (form.gigantamax && form.spriteGigantamax) {
      tiles.push({
        key: `${form.id}-gmax`,
        label: t('pokemon.versionsTab.gigantamax'),
        sources: pictureSources(form, { gigantamax: true }),
      })
    } else if (form.dynamax && form.isDefault) {
      tiles.push({
        key: `${form.id}-dmax`,
        label: t('pokemon.versionsTab.dynamax'),
        sources: pictureSources(form),
      })
    }
  }
  const shown = version.form
  return (
    <Section id="versions" title={t('pokemon.versionsTab.title')}>
      <Card>
        <ul className="pk-tiles">
          {tiles.map((tile) => (
            <li key={tile.key} className={tile.className}>
              <SpriteBox src={tile.sources} alt="" size={64} />
              <span>{tile.label}</span>
            </li>
          ))}
        </ul>
        <p className="pk-note">
          {t('pokemon.versionsTab.details', {
            name: formName(shown, page, language),
            attack: formats.number(shown.attack),
            defense: formats.number(shown.defense),
            stamina: formats.number(shown.stamina),
            cp: formats.number(shown.maxCp.find((row) => row.level === 50)?.cp ?? 0),
          })}
        </p>
        <h3 className="pk-label">{t('pokemon.versionsTab.costumes')}</h3>
        {page.costumes.length === 0 ? (
          <p className="pk-muted">{t('pokemon.versionsTab.noCostumes')}</p>
        ) : (
          <ul className="pk-tiles">
            {page.costumes.map((costume) => (
              <li key={costume.sprite}>
                <SpriteBox src={[spriteUrl(costume.sprite)]} alt="" size={64} />
                <span>{costume.nameEn}</span>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </Section>
  )
}
