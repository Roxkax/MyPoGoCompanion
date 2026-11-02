import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { ApiError } from '../api/client'
import { LoadError } from '../components/LoadError'
import { StaleBar } from '../freshness/StaleWarnings'
import { useFormats } from '../i18n/languageContext'
import { DetailScreen } from '../layout/Screen'
import { Card } from '../ui/Card'
import { Chip } from '../ui/Chip'
import { Pill } from '../ui/Pill'
import { SegmentedSwitch } from '../ui/SegmentedSwitch'
import { SpriteBox } from '../ui/SpriteBox'
import { TypeBadge } from '../ui/TypeBadge'
import { VersionMarker } from '../ui/VersionMarker'
import { pokemonTypes, versionKinds, type PokemonType } from '../ui/pokemonTypes'

/**
 * /design, on QA and local copies only: every shared component in one place, to check the design system in light
 * and dark before the screens that use them exist.
 */
export function DesignScreen() {
  const [kind, setKind] = useState<'fast' | 'charged'>('charged')
  const [shiny, setShiny] = useState(true)
  const [type, setType] = useState<PokemonType | null>('dragon')
  const { t } = useTranslation()
  const formats = useFormats()
  // A fixed moment, so the gallery shows the same example every day: Saturday 31 October 2026, 20:00 on this device.
  const sample = new Date(2026, 9, 31, 20, 0)

  return (
    <DetailScreen back={{ to: '/', label: 'today' }} title="designSystem">
      <div className="stack">
        <Card as="section" aria-labelledby="design-stale">
          <h2 id="design-stale" className="group-label">
            {t('design.staleWarnings')}
          </h2>
          <StaleBar kind="live" days={12} />
          <StaleBar kind="game" days={4} />
        </Card>
        <section className="stack" aria-labelledby="design-errors">
          <h2 id="design-errors" className="group-label">
            {t('design.errors')}
          </h2>
          <LoadError error={new ApiError(500, 'Example', '4bf92f35')} onRetry={() => {}} />
          <Card>
            <p className="design-fail-links">
              {t('design.tryErrors')} <Link to="/pokedex?fail=screen">?fail=screen</Link> ·{' '}
              <Link to="/?fail=api">?fail=api</Link> ·{' '}
              <Link to="/design?fail=script">?fail=script</Link>
            </p>
          </Card>
        </section>
        <Card as="section" aria-labelledby="design-types">
          <h2 id="design-types" className="group-label">
            {t('design.typeBadges')}
          </h2>
          <div className="wrap-row">
            {pokemonTypes.map((t) => (
              <TypeBadge key={t} type={t} />
            ))}
          </div>
          <div className="wrap-row">
            <TypeBadge type="dragon" size="large" />
            <TypeBadge type="ground" size="large" />
          </div>
        </Card>
        <Card as="section" aria-labelledby="design-versions">
          <h2 id="design-versions" className="group-label">
            {t('design.versionMarkers')}
          </h2>
          <div className="wrap-row">
            {versionKinds.map((k) => (
              <VersionMarker key={k} kind={k} />
            ))}
          </div>
        </Card>
        <Card as="section" aria-labelledby="design-pills">
          <h2 id="design-pills" className="group-label">
            {t('design.pills')}
          </h2>
          <div className="wrap-row">
            <Pill tone="live" dot>
              {t('design.liveNow')}
            </Pill>
            <Pill>{t('design.endsAt', { when: formats.dayTime(sample) })}</Pill>
            <Pill tone="accent">{t('profile.admin')}</Pill>
            <Pill tone="warn">{t('design.outOfDate')}</Pill>
            <Pill tone="danger">{t('design.gone')}</Pill>
            <Pill tone="gold">{t('design.lucky')}</Pill>
            <Pill tone="raid">{t('design.raidShare', { share: formats.percent(0.97) })}</Pill>
          </div>
        </Card>
        <Card as="section" aria-labelledby="design-chips">
          <h2 id="design-chips" className="group-label">
            {t('design.chips')}
          </h2>
          <div className="wrap-row">
            {type && <Chip onRemove={() => setType(null)}>{t(`types.${type}`)}</Chip>}
            <Chip selected={shiny} onClick={() => setShiny((on) => !on)}>
              {t('design.shiny')}
            </Chip>
            <Chip>{t('design.released')}</Chip>
          </div>
          <SegmentedSwitch
            label={t('design.moveKind')}
            value={kind}
            onChange={setKind}
            options={[
              { value: 'fast', label: t('design.fast'), count: 80 },
              { value: 'charged', label: t('design.charged'), count: 1235 },
            ]}
          />
        </Card>
        <Card as="section" aria-labelledby="design-sprites">
          <h2 id="design-sprites" className="group-label">
            {t('design.sprites')}
          </h2>
          <div className="wrap-row">
            <SpriteBox src="/icon.svg" alt={t('design.appIcon')} size={64} />
            <SpriteBox alt={t('design.noPicture')} size={64} />
            <SpriteBox src="/missing-sprite.png" alt={t('design.brokenPicture')} size={40} />
          </div>
        </Card>
        <Card padded={false} as="section" aria-labelledby="design-card">
          <h2 id="design-card" className="group-label card-rows-title">
            {t('design.cardRows')}
          </h2>
          <ul className="card-rows">
            <li>{t('design.greatLeague')}</li>
            <li>{t('design.ultraLeague')}</li>
            <li>{t('design.masterLeague')}</li>
          </ul>
        </Card>
        <Card as="section" aria-labelledby="design-formats">
          <h2 id="design-formats" className="group-label">
            {t('design.formats')}
          </h2>
          <ul className="design-formats">
            <li>{formats.longDay(sample, true)}</li>
            <li>{formats.dayTime(sample)}</li>
            <li className="num">
              {formats.number(1234.5)} · {formats.number(12345.5)} · {formats.percent(0.975, 1)}
            </li>
            <li>
              {t('design.daysLeft', { count: 1 })} · {t('design.daysLeft', { count: 3 })}
            </li>
          </ul>
        </Card>
      </div>
    </DetailScreen>
  )
}
