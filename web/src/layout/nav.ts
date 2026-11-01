import type { NavKey, SectionKey } from '../i18n/keys'
import type { IconName } from '../components/icons'

export type TabKey = 'today' | 'pokedex' | 'battle' | 'filters' | 'collection'

export interface NavItem<K extends NavKey = NavKey> {
  /** Also the item's text: nav.<key>. */
  key: K
  icon: IconName
  to: string
}

/** A section in a tab's switcher; its text is sections.<section>. */
export interface NavSection {
  to: string
  section: SectionKey
  /** Only this exact address counts as this section (not the addresses below it). */
  end?: boolean
}

/** The five tabs (app map): the phone's bottom tab bar and the top of the desktop sidebar. */
export const tabs: readonly NavItem<TabKey>[] = [
  { key: 'today', icon: 'today', to: '/' },
  { key: 'pokedex', icon: 'pokedex', to: '/pokedex' },
  { key: 'battle', icon: 'battle', to: '/battle/raids' },
  { key: 'filters', icon: 'filters', to: '/filters' },
  { key: 'collection', icon: 'collection', to: '/collection' },
]

export type QuickLinkKey = 'calendar' | 'eggs' | 'moves' | 'tiers'

/** The desktop sidebar's quick links, under the tabs. */
export const quickLinks: readonly NavItem<QuickLinkKey>[] = [
  { key: 'calendar', icon: 'calendar', to: '/calendar' },
  { key: 'eggs', icon: 'egg', to: '/eggs' },
  { key: 'moves', icon: 'moves', to: '/pokedex/moves' },
  { key: 'tiers', icon: 'chart', to: '/battle/tiers' },
]

/** Pokédex: Pokémon · Moves · Type chart (on the phone; the desktop reaches them from the sidebar). */
export const pokedexSections = [
  { to: '/pokedex', section: 'pokemon', end: true },
  { to: '/pokedex/moves', section: 'moves', end: true },
  { to: '/pokedex/types', section: 'typeChart', end: true },
] as const satisfies readonly NavSection[]

/** Battle: Raids · Tier lists · Teams. */
export const battleSections = [
  { to: '/battle/raids', section: 'raids', end: true },
  { to: '/battle/tiers', section: 'tierLists' },
  { to: '/battle/teams', section: 'teams', end: true },
] as const satisfies readonly NavSection[]

/** Filters: Keepers · Builder · Saved. */
export const filterSections = [
  { to: '/filters', section: 'keepers', end: true },
  { to: '/filters/builder', section: 'builder', end: true },
  { to: '/filters/saved', section: 'saved', end: true },
] as const satisfies readonly NavSection[]

function segments(pathname: string): string[] {
  return pathname.split('/').filter(Boolean)
}

/** The tab an address belongs to (lit in the tab bar); null for Settings, Admin and pages outside the tabs. */
export function activeTab(pathname: string): TabKey | null {
  const [first] = segments(pathname)
  switch (first) {
    case undefined:
    case 'calendar':
    case 'eggs':
      return 'today'
    case 'pokedex':
    case 'battle':
    case 'filters':
    case 'collection':
      return first
    default:
      return null
  }
}

export type SidebarKey = TabKey | QuickLinkKey | 'settings' | 'admin'

/**
 * The one sidebar entry lit for an address: a quick link when the page is one (Calendar, Moves…), otherwise its tab,
 * Settings or Admin. Matches the approved desktop screens (e.g. A2 lights Calendar, not Today).
 */
export function activeSidebarItem(pathname: string): SidebarKey | null {
  const [first, second] = segments(pathname)
  if (first === 'calendar') return 'calendar'
  if (first === 'eggs') return 'eggs'
  if (first === 'pokedex' && second === 'moves') return 'moves'
  if (first === 'battle' && second === 'tiers') return 'tiers'
  if (first === 'settings') return 'settings'
  if (first === 'admin') return 'admin'
  return activeTab(pathname)
}
