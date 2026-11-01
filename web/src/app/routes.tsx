import type { ReactNode } from 'react'
import { Navigate, type RouteObject } from 'react-router'
import type { AppEnvironment } from '../environment'
import type { SoonKey, TitleKey } from '../i18n/keys'
import { AppShell, type ScreenHandle } from '../layout/AppShell'
import { battleSections, filterSections, pokedexSections } from '../layout/nav'
import { DetailScreen, TabScreen, type BackTarget, type Sections } from '../layout/Screen'
import { AllChangesScreen } from '../screens/AllChangesScreen'
import { CallbackRoute } from '../screens/CallbackRoute'
import { ComingSoon } from '../screens/ComingSoon'
import { DesignScreen } from '../screens/DesignScreen'
import { NotFoundScreen } from '../screens/NotFoundScreen'
import { RouteError } from '../screens/RouteError'
import { SettingsScreen } from '../screens/SettingsScreen'
import { SignInRoute } from '../screens/SignInRoute'
import { TodayScreen } from '../screens/TodayScreen'
import { AdminOnly, SignedInOnly } from './guards'

/*
 * Every screen's address (decision 2026-11-01): English names in lowercase whatever the app language, filters after
 * "?". Screens not built yet show "Coming soon" inside the real shell; each feature swaps in its screen here.
 * Titles and labels here are keys of the texts (titles.<key>), shown in the app's language.
 */

const pokedex: Sections = { label: 'pokedex', links: pokedexSections }
const battle: Sections = { label: 'battle', links: battleSections, onDesktop: true }
const filters: Sections = { label: 'filters', links: filterSections, onDesktop: true }

/** A screen not built yet: its texts (soon.<id>), its code in the design and the feature that builds it. */
function soon(id: SoonKey, screen: string, feature: string): ReactNode {
  return <ComingSoon id={id} screen={screen} feature={feature} />
}

function tab(
  frame: { title: TitleKey; desktopTitle?: TitleKey; sections?: Sections; search?: boolean },
  content: ReactNode,
): ReactNode {
  return <TabScreen {...frame}>{content}</TabScreen>
}

function detail(back: BackTarget, title: TitleKey | undefined, content: ReactNode): ReactNode {
  return (
    <DetailScreen back={back} title={title}>
      {content}
    </DetailScreen>
  )
}

const today: BackTarget = { to: '/', label: 'today' }
const calendar: BackTarget = { to: '/calendar', label: 'calendar' }
const pokedexBack: BackTarget = { to: '/pokedex', label: 'pokedex' }
const moves: BackTarget = { to: '/pokedex/moves', label: 'moves' }
const raids: BackTarget = { to: '/battle/raids', label: 'raids' }
const collection: BackTarget = { to: '/collection', label: 'collection' }

function screen(path: string, element: ReactNode, handle: ScreenHandle): RouteObject {
  return { path, element, handle }
}

/** The app's routes. `/design` (the component gallery) exists only on QA and local copies. */
export function appRoutes(environment: AppEnvironment): RouteObject[] {
  const children: RouteObject[] = [
    // A. Today
    { index: true, element: <TodayScreen />, handle: {} satisfies ScreenHandle },
    screen('calendar', detail(today, 'calendar', soon('calendar', 'A2', 'E06-F02 Calendar')), {
      title: 'calendar',
    }),
    screen(
      'calendar/:event',
      detail(calendar, 'event', soon('event', 'A3', 'E06-F03 Event details')),
      { title: 'event' },
    ),
    screen('eggs', detail(today, 'eggs', soon('eggs', 'A4', 'E06-F04 Eggs & research')), {
      title: 'eggs',
    }),

    // B. Pokédex
    screen(
      'pokedex',
      tab(
        { title: 'pokedex', sections: pokedex, search: true },
        soon('pokedex', 'B1', 'E03-F01 Pokédex list'),
      ),
      { title: 'pokedex' },
    ),
    screen(
      'pokedex/moves',
      tab(
        { title: 'pokedex', desktopTitle: 'moves', sections: pokedex },
        soon('moves', 'B3', 'E03-F03 Moves list'),
      ),
      { title: 'moves' },
    ),
    screen(
      'pokedex/moves/:move',
      detail(moves, undefined, soon('move', 'B4', 'E03-F04 Move page')),
      { title: 'move' },
    ),
    screen(
      'pokedex/types',
      tab(
        { title: 'pokedex', desktopTitle: 'typeChart', sections: pokedex },
        soon('typeChart', 'B5', 'E03-F05 Type chart'),
      ),
      { title: 'typeChart' },
    ),
    screen(
      'pokedex/:pokemon',
      detail(pokedexBack, undefined, soon('pokemon', 'B2', 'E03-F02 Pokémon page')),
      { title: 'pokemon' },
    ),

    // C. Battle
    { path: 'battle', element: <Navigate to="/battle/raids" replace /> },
    screen(
      'battle/raids',
      tab({ title: 'battle', sections: battle }, soon('raids', 'C1', 'E08-F01 Raid bosses')),
      { title: 'raids' },
    ),
    screen(
      'battle/raids/:boss',
      detail(raids, undefined, soon('raidBoss', 'C2', 'E08-F02 Raid boss page')),
      { title: 'raidBoss' },
    ),
    { path: 'battle/tiers', element: <Navigate to="/battle/tiers/great-league" replace /> },
    ...['battle/tiers/raids/:type?', 'battle/tiers/:league'].map((path) =>
      screen(
        path,
        tab({ title: 'battle', sections: battle }, soon('tierLists', 'C3', 'E08-F03 Tier lists')),
        { title: 'tierLists' },
      ),
    ),
    screen(
      'battle/teams',
      tab({ title: 'battle', sections: battle }, soon('teams', 'C4', 'E08-F04 Best team')),
      { title: 'teams' },
    ),

    // D. Filters
    screen(
      'filters',
      tab({ title: 'filters', sections: filters }, soon('keepers', 'D1', 'E04-F01 Keeper filters')),
      { title: 'filters' },
    ),
    screen(
      'filters/builder',
      tab({ title: 'filters', sections: filters }, soon('builder', 'D2', 'E04-F04 Filter builder')),
      { title: 'filterBuilder' },
    ),
    screen(
      'filters/saved',
      tab(
        { title: 'filters', sections: filters },
        <SignedInOnly feature="savedFilters">
          {soon('saved', 'D3', 'E04-F05 Saved filters')}
        </SignedInOnly>,
      ),
      { title: 'savedFilters' },
    ),

    // E. Collection (signed in only)
    screen(
      'collection',
      tab(
        { title: 'collection' },
        <SignedInOnly feature="collection">
          {soon('collection', 'E1', 'E07-F03 Collection list')}
        </SignedInOnly>,
      ),
      { title: 'collection' },
    ),
    screen(
      'collection/upload',
      detail(
        collection,
        'upload',
        <SignedInOnly feature="collection">
          {soon('upload', 'E2', 'E07-F01 Poke Genie import')}
        </SignedInOnly>,
      ),
      { title: 'upload' },
    ),
    screen(
      'collection/imports/:id',
      detail(
        collection,
        'importReport',
        <SignedInOnly feature="collection">
          {soon('importReport', 'E2', 'E07-F02 Re-upload and what changed')}
        </SignedInOnly>,
      ),
      { title: 'importReport' },
    ),
    {
      path: 'collection/imports/:id/changes',
      handle: { title: 'allChanges' } satisfies ScreenHandle,
      Component: AllChangesScreen,
    },

    // F. Me
    {
      path: 'sign-in',
      element: <SignInRoute />,
      handle: { title: 'signIn' } satisfies ScreenHandle,
    },
    screen('settings', <SettingsScreen />, { title: 'settings', hideTabBar: true }),

    // G. Admin (admins only; everyone else gets "Page not found"). Back returns to where the admin came from (the
    // profile menu or the sidebar): a fixed "‹ Settings" would bounce between Admin and Settings, which both hide the
    // phone's tab bar. The design's "‹ Settings" fits once Settings has its Admin row (E09-F01).
    screen(
      'admin',
      detail(
        'history',
        'admin',
        <AdminOnly>{soon('dataStatus', 'G1', 'E10-F01 Data status and re-run')}</AdminOnly>,
      ),
      { title: 'admin', hideTabBar: true },
    ),
    screen(
      'admin/entries',
      detail(
        'history',
        'admin',
        <AdminOnly>{soon('entries', 'G2', 'E10-F02 Fix fetched entries')}</AdminOnly>,
      ),
      { title: 'admin', hideTabBar: true },
    ),

    // Google sends people back here after signing in.
    screen('auth/callback', <CallbackRoute />, { title: 'signingIn', hideTabBar: true }),
  ]

  if (environment.kind !== 'production') {
    children.push(screen('design', <DesignScreen />, { title: 'designSystem' }))
  }

  children.push(screen('*', <NotFoundScreen />, { title: 'pageNotFound' }))

  return [
    {
      path: '/',
      element: <AppShell environment={environment} />,
      errorElement: <RouteError />,
      children,
    },
  ]
}
