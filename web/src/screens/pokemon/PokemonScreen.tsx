import { useMemo, type CSSProperties, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router'
import { $api } from '../../api/client'
import { LoadError } from '../../components/LoadError'
import { useCurrentLanguage, useFormats } from '../../i18n/languageContext'
import { DetailScreen } from '../../layout/Screen'
import { useIsDesktop } from '../../layout/useMediaQuery'
import { dexForSlug, pickVersion, typesOf, versionsOf, type PageVersion } from '../../pokemon/page'
import { Card } from '../../ui/Card'
import { pictureSources } from '../../ui/sprites'
import { SpriteBox } from '../../ui/SpriteBox'
import { TypeBadge } from '../../ui/TypeBadge'
import { formName, versionLabel } from './names'
import {
  BestIvsSection,
  CheatSheetSection,
  CheckIvsSection,
  EvolutionSection,
  GetItSection,
  MatchupsSection,
  MovesSection,
  PvpUsageSection,
  RaidMovesetsSection,
  RankingsSection,
  StatsSection,
  VersionsSection,
  type SectionProps,
} from './sections'

/** Browsers keep the Pokédex and a Pokémon's page an hour (the API's cache); the app doesn't ask again sooner. */
const staleMs = 60 * 60 * 1000

const pokemonTabs = ['overview', 'moves', 'ivs', 'getIt', 'versions'] as const
type PokemonTab = (typeof pokemonTabs)[number]

/** The phone tab in the address's `#` (`#moves`), so Back returns to it; Overview without one. */
function tabOf(hash: string): PokemonTab {
  const wanted = hash.replace(/^#/, '')
  return (pokemonTabs as readonly string[]).includes(wanted) ? (wanted as PokemonTab) : 'overview'
}

/**
 * B2 Pokémon page (E03-F02): one species with all its versions, from one answer (GET /pokedex/{dex}). The address
 * names the species in English (`/pokedex/garchomp`), the version in `?version=` and the phone tab in `#`.
 */
export function PokemonScreen() {
  const { pokemon = '' } = useParams()
  const { t } = useTranslation()
  const pokedex = $api.useQuery('get', '/pokedex', undefined, { staleTime: staleMs })
  const dex = useMemo(
    () => (pokedex.data ? dexForSlug(pokedex.data.species, pokemon) : null),
    [pokedex.data, pokemon],
  )
  const page = $api.useQuery(
    'get',
    '/pokedex/{dex}',
    { params: { path: { dex: dex ?? 0 } } },
    { enabled: dex !== null, staleTime: staleMs },
  )

  const back = { to: '/pokedex', label: 'pokedex' } as const
  const error = pokedex.error ?? page.error
  if (error) {
    return (
      <DetailScreen back={back}>
        <LoadError
          error={error}
          onRetry={() => {
            void pokedex.refetch()
            if (dex !== null) void page.refetch()
          }}
        />
      </DetailScreen>
    )
  }
  if (pokedex.data && dex === null) {
    return (
      <DetailScreen back={back}>
        <Card as="section" className="not-found">
          <h2>{t('titles.pageNotFound')}</h2>
          <p>{t('pokemon.notFound', { name: decodeURIComponent(pokemon) })}</p>
          <Link to="/pokedex" className="button">
            {t('pokemon.backToPokedex')}
          </Link>
        </Card>
      </DetailScreen>
    )
  }
  if (!page.data) {
    return (
      <DetailScreen back={back}>
        <p className="pk-loading" role="status">
          {t('pokemon.loading')}
        </p>
      </DetailScreen>
    )
  }
  return <PokemonPageView page={page.data} />
}

function PokemonPageView({ page }: { page: SectionProps['page'] }) {
  const { t } = useTranslation()
  const desktop = useIsDesktop()
  const language = useCurrentLanguage()
  const formats = useFormats()
  const navigate = useNavigate()
  const location = useLocation()
  const [params, setParams] = useSearchParams()
  const versions = useMemo(() => versionsOf(page), [page])
  const version = pickVersion(versions, params.get('version'))
  const tab = tabOf(location.hash)

  if (!version) return null
  const props: SectionProps = { page, version }
  const types = typesOf(version.form)
  const name = version.shadow
    ? t('pokemon.shadowOf', { name: formName(version.form, page, language) })
    : formName(version.form, page, language)

  const choose = (next: PageVersion) => {
    const nextParams = new URLSearchParams(params)
    if (next === versions[0]) nextParams.delete('version')
    else nextParams.set('version', next.key)
    void setParams(nextParams, { replace: true, preventScrollReset: true })
  }

  const head = (
    <div className="pk-head" data-shadow={version.shadow ? true : undefined}>
      <SpriteBox src={pictureSources(version.form)} alt={name} size={desktop ? 150 : 104} />
      <div className="pk-head-text">
        <p className="pk-number">#{formats.number(page.dex, { useGrouping: false })}</p>
        <h1 className="pk-name">{name}</h1>
        <div className="pk-types">
          {types.map((type) => (
            <TypeBadge key={type} type={type} />
          ))}
          {!version.form.released && <span className="pk-unreleased">{t('pokemon.notInGo')}</span>}
        </div>
      </div>
      {versions.length > 1 && (
        <div className="pk-switcher" role="group" aria-label={t('pokemon.versionsLabel')}>
          {versions.map((option) => (
            <button
              key={option.key}
              type="button"
              className="pk-version"
              aria-pressed={option === version}
              onClick={() => choose(option)}
            >
              {versionLabel(option, page, language, t)}
            </button>
          ))}
        </div>
      )}
    </div>
  )

  const tint = { '--tint': `var(--t-${types[0] ?? 'normal'})` } as CSSProperties

  if (desktop) {
    return (
      <DetailScreen back={{ to: '/pokedex', label: 'pokedex' }} tint={types[0]}>
        <div className="pk-band" style={tint}>
          {head}
        </div>
        <div className="pk-columns">
          <div className="pk-column">
            <StatsSection {...props} />
            <RankingsSection {...props} />
            <MovesSection {...props} />
            <RaidMovesetsSection {...props} />
            <PvpUsageSection {...props} />
            <VersionsSection {...props} />
          </div>
          <div className="pk-column">
            <BestIvsSection {...props} />
            <CheckIvsSection {...props} />
            <CheatSheetSection {...props} />
            <MatchupsSection {...props} />
            <EvolutionSection {...props} />
            <GetItSection {...props} />
          </div>
        </div>
      </DetailScreen>
    )
  }

  const panels: Record<PokemonTab, ReactNode> = {
    overview: (
      <>
        <StatsSection {...props} />
        <RankingsSection {...props} />
        <MatchupsSection {...props} />
      </>
    ),
    moves: (
      <>
        <MovesSection {...props} />
        <RaidMovesetsSection {...props} />
        <PvpUsageSection {...props} />
      </>
    ),
    ivs: (
      <>
        <BestIvsSection {...props} />
        <CheckIvsSection {...props} />
        <CheatSheetSection {...props} />
      </>
    ),
    getIt: (
      <>
        <GetItSection {...props} />
        <EvolutionSection {...props} />
      </>
    ),
    versions: <VersionsSection {...props} />,
  }

  return (
    <DetailScreen back={{ to: '/pokedex', label: 'pokedex' }} tint={types[0]}>
      <div className="pk-band" style={tint}>
        {head}
        <nav className="pk-tabs" aria-label={t('pokemon.tabsLabel')}>
          {pokemonTabs.map((key) => (
            <button
              key={key}
              type="button"
              className="pk-tab"
              aria-current={key === tab ? 'page' : undefined}
              onClick={() =>
                void navigate(
                  { search: location.search, hash: key === 'overview' ? '' : `#${key}` },
                  { preventScrollReset: true },
                )
              }
            >
              {t(`pokemon.tabs.${key}`)}
            </button>
          ))}
        </nav>
      </div>
      <div className="stack pk-panel">{panels[tab]}</div>
    </DetailScreen>
  )
}
