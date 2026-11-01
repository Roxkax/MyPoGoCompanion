import type { IconName } from '../components/icons'

export type TabKey = 'today' | 'pokedex' | 'battle' | 'filters' | 'collection'

export interface NavItem<K extends string = string> {
  key: K
  label: string
  icon: IconName
  to: string
}

/** The five tabs (app map): the phone's bottom tab bar and the top of the desktop sidebar. */
export const tabs: readonly NavItem<TabKey>[] = [
  { key: 'today', label: 'Today', icon: 'today', to: '/' },
  { key: 'pokedex', label: 'Pokédex', icon: 'pokedex', to: '/pokedex' },
  { key: 'battle', label: 'Battle', icon: 'battle', to: '/battle/raids' },
  { key: 'filters', label: 'Filters', icon: 'filters', to: '/filters' },
  { key: 'collection', label: 'Collection', icon: 'collection', to: '/collection' },
]

export type QuickLinkKey = 'calendar' | 'eggs' | 'moves' | 'tiers'

/** The desktop sidebar's quick links, under the tabs. */
export const quickLinks: readonly NavItem<QuickLinkKey>[] = [
  { key: 'calendar', label: 'Calendar', icon: 'calendar', to: '/calendar' },
  { key: 'eggs', label: 'Eggs & research', icon: 'egg', to: '/eggs' },
  { key: 'moves', label: 'Moves', icon: 'moves', to: '/pokedex/moves' },
  { key: 'tiers', label: 'Tier lists', icon: 'chart', to: '/battle/tiers' },
]

/** Pokédex: Pokémon · Moves · Type chart (on the phone; the desktop reaches them from the sidebar). */
export const pokedexSections = [
  { to: '/pokedex', label: 'Pokémon', end: true },
  { to: '/pokedex/moves', label: 'Moves', end: true },
  { to: '/pokedex/types', label: 'Type chart', end: true },
] as const

/** Battle: Raids · Tier lists · Teams. */
export const battleSections = [
  { to: '/battle/raids', label: 'Raids', end: true },
  { to: '/battle/tiers', label: 'Tier lists' },
  { to: '/battle/teams', label: 'Teams', end: true },
] as const

/** Filters: Keepers · Builder · Saved. */
export const filterSections = [
  { to: '/filters', label: 'Keepers', end: true },
  { to: '/filters/builder', label: 'Builder', end: true },
  { to: '/filters/saved', label: 'Saved', end: true },
] as const

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
