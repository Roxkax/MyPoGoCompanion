import type { CSSProperties, ReactNode } from 'react'
import { Link, useLocation, useNavigate } from 'react-router'
import { ProfileButton } from '../components/ProfileButton'
import { Icon } from '../components/icons'
import { SegmentedLinks, type SegmentLink } from '../ui/SegmentedLinks'
import type { PokemonType } from '../ui/pokemonTypes'
import { AskButton } from './AskButton'
import { SearchButton } from './SearchButton'
import { useIsDesktop } from './useMediaQuery'

export interface Sections {
  /** What the switcher chooses between, for screen readers (e.g. "Pokédex sections"). */
  label: string
  links: readonly SegmentLink[]
  /** Also shown on the desktop (Battle, Filters); the Pokédex's sections are sidebar links there. */
  onDesktop?: boolean
}

interface TabScreenProps {
  /** The large title (phone): the tab's name. */
  title: string
  /** The desktop's title when it differs (e.g. "Moves" inside the Pokédex tab). */
  desktopTitle?: string
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

  if (desktop) {
    return (
      <main className="screen">
        <div className="page-head">
          <div className="page-head-titles">
            <h1 className="page-title">{desktopTitle ?? title}</h1>
            {subtitle && <p className="page-subtitle">{subtitle}</p>}
          </div>
          {sections?.onDesktop && <SegmentedLinks label={sections.label} links={sections.links} />}
        </div>
        {children}
      </main>
    )
  }

  return (
    <>
      <header className="tab-head">
        <div className="tab-head-row">
          <h1 className="tab-title">{title}</h1>
          {actions}
          <AskButton />
          <ProfileButton />
        </div>
        {subtitle && <p className="tab-subtitle">{subtitle}</p>}
        {sections && <SegmentedLinks label={sections.label} links={sections.links} />}
        {search && <SearchButton />}
      </header>
      <main className="screen">{children}</main>
    </>
  )
}

/** Where a detail screen's back button goes: a fixed parent screen, or the previous page ("Back"). */
export type BackTarget = { to: string; label: string } | 'history'

/** "‹ Pokédex": back to the screen this one belongs to; "‹ Back": back to wherever the person came from. */
export function BackLink({ back }: { back: BackTarget }) {
  const navigate = useNavigate()
  const location = useLocation()

  if (back === 'history') {
    return (
      <button
        type="button"
        className="back-link"
        onClick={() => (location.key !== 'default' ? navigate(-1) : navigate('/'))}
      >
        <Icon name="back" size={18} strokeWidth={2.6} />
        Back
      </button>
    )
  }
  return (
    <Link to={back.to} className="back-link">
      <Icon name="back" size={18} strokeWidth={2.6} />
      {back.label}
    </Link>
  )
}

interface DetailScreenProps {
  back: BackTarget
  /** The screen's name; left out where the content starts with its own big name (Pokémon, raid boss). */
  title?: string
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
  const tintStyle = tint ? ({ '--tint': `var(--t-${tint})` } as CSSProperties) : undefined

  if (desktop) {
    return (
      <main className="screen" data-tinted={tint ? true : undefined} style={tintStyle}>
        <div className="page-head page-head-detail">
          <BackLink back={back} />
          {title && <h1 className="page-title">{title}</h1>}
        </div>
        {children}
      </main>
    )
  }

  return (
    <>
      <header className="detail-head" data-tinted={tint ? true : undefined} style={tintStyle}>
        <BackLink back={back} />
        {title ? <h1 className="detail-title">{title}</h1> : <span />}
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
