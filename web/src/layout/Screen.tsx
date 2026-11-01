import type { CSSProperties, ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useLocation, useNavigate } from 'react-router'
import { ProfileButton } from '../components/ProfileButton'
import { Icon } from '../components/icons'
import type { SectionKey, TitleKey } from '../i18n/keys'
import { SegmentedLinks, type SegmentLink } from '../ui/SegmentedLinks'
import type { PokemonType } from '../ui/pokemonTypes'
import { AskButton } from './AskButton'
import type { NavSection } from './nav'
import { SearchButton } from './SearchButton'
import { useIsDesktop } from './useMediaQuery'

export interface Sections {
  /** What the switcher chooses between, for screen readers: sections.<label> (e.g. "Pokédex sections"). */
  label: SectionKey
  links: readonly NavSection[]
  /** Also shown on the desktop (Battle, Filters); the Pokédex's sections are sidebar links there. */
  onDesktop?: boolean
}

interface TabScreenProps {
  /** The large title (phone): the tab's name (titles.<title>). */
  title: TitleKey
  /** The desktop's title when it differs (e.g. "Moves" inside the Pokédex tab). */
  desktopTitle?: TitleKey
  subtitle?: ReactNode
  /** Extra round buttons before Ask in the phone header (e.g. Today's calendar button). */
  actions?: ReactNode
  sections?: Sections
  /** The search bar under the title (phone; the desktop has it in the top bar). */
  search?: boolean
  children: ReactNode
}

/**
 * A tab's own screen (A1, B1, C1, D1, E1 and their sections). Phone: large title, Ask and profile, optional section
 * switcher and search bar. Desktop: the title and switcher at the top of the page; search, Ask and profile are in
 * the top bar.
 */
export function TabScreen({
  title,
  desktopTitle,
  subtitle,
  actions,
  sections,
  search = false,
  children,
}: TabScreenProps) {
  const desktop = useIsDesktop()
  const { t } = useTranslation()
  const switcher = sections && (
    <SegmentedLinks
      label={t(`sections.${sections.label}`)}
      links={sections.links.map((link): SegmentLink => ({
        to: link.to,
        end: link.end,
        label: t(`sections.${link.section}`),
      }))}
    />
  )

  if (desktop) {
    return (
      <main className="screen">
        <div className="page-head">
          <div className="page-head-titles">
            <h1 className="page-title">{t(`titles.${desktopTitle ?? title}`)}</h1>
            {subtitle && <p className="page-subtitle">{subtitle}</p>}
          </div>
          {sections?.onDesktop && switcher}
        </div>
        {children}
      </main>
    )
  }

  return (
    <>
      <header className="tab-head">
        <div className="tab-head-row">
          <h1 className="tab-title">{t(`titles.${title}`)}</h1>
          {actions}
          <AskButton />
          <ProfileButton />
        </div>
        {subtitle && <p className="tab-subtitle">{subtitle}</p>}
        {switcher}
        {search && <SearchButton />}
      </header>
      <main className="screen">{children}</main>
    </>
  )
}

/** Where a detail screen's back button goes: a fixed parent screen, or the previous page ("Back"). */
export type BackTarget = { to: string; label: TitleKey } | 'history'

/** "‹ Pokédex": back to the screen this one belongs to; "‹ Back": back to wherever the person came from. */
export function BackLink({ back }: { back: BackTarget }) {
  const navigate = useNavigate()
  const location = useLocation()
  const { t } = useTranslation()

  if (back === 'history') {
    return (
      <button
        type="button"
        className="back-link"
        onClick={() => (location.key !== 'default' ? navigate(-1) : navigate('/'))}
      >
        <Icon name="back" size={18} strokeWidth={2.6} />
        {t('header.back')}
      </button>
    )
  }
  return (
    <Link to={back.to} className="back-link">
      <Icon name="back" size={18} strokeWidth={2.6} />
      {t(`titles.${back.label}`)}
    </Link>
  )
}

interface DetailScreenProps {
  back: BackTarget
  /** The screen's name (titles.<title>); left out where the content starts with its own big name (Pokémon, raid boss). */
  title?: TitleKey
  /** Pokémon and raid boss pages are tinted with the main type (design conventions). */
  tint?: PokemonType
  children: ReactNode
}

/**
 * Any screen below a tab (Calendar, a Pokémon, a raid boss, Settings…). Phone: a header with the back button, the
 * screen's name and Ask. Desktop: the back link and the title at the top of the page.
 */
export function DetailScreen({ back, title, tint, children }: DetailScreenProps) {
  const desktop = useIsDesktop()
  const { t } = useTranslation()
  const name = title && t(`titles.${title}`)
  const tintStyle = tint ? ({ '--tint': `var(--t-${tint})` } as CSSProperties) : undefined

  if (desktop) {
    return (
      <main className="screen" data-tinted={tint ? true : undefined} style={tintStyle}>
        <div className="page-head page-head-detail">
          <BackLink back={back} />
          {name && <h1 className="page-title">{name}</h1>}
        </div>
        {children}
      </main>
    )
  }

  return (
    <>
      <header className="detail-head" data-tinted={tint ? true : undefined} style={tintStyle}>
        <BackLink back={back} />
        {name ? <h1 className="detail-title">{name}</h1> : <span />}
        <span className="detail-head-end">
          <AskButton />
        </span>
      </header>
      <main className="screen" data-tinted={tint ? true : undefined} style={tintStyle}>
        {children}
      </main>
    </>
  )
}
