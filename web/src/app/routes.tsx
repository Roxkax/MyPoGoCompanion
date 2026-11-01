import type { ReactNode } from 'react'
import { Navigate, type RouteObject } from 'react-router'
import type { AppEnvironment } from '../environment'
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
 */

const pokedex: Sections = { label: 'Pokédex sections', links: pokedexSections }
const battle: Sections = { label: 'Battle sections', links: battleSections, onDesktop: true }
const filters: Sections = { label: 'Filter sections', links: filterSections, onDesktop: true }

interface Soon {
  screen: string
  name: string
  feature: string
  text: string
}

function soon(info: Soon): ReactNode {
  return <ComingSoon {...info} />
}

function tab(
  frame: { title: string; desktopTitle?: string; sections?: Sections; search?: boolean },
  content: ReactNode,
): ReactNode {
  return <TabScreen {...frame}>{content}</TabScreen>
}

function detail(back: BackTarget, title: string | undefined, content: ReactNode): ReactNode {
  return (
    <DetailScreen back={back} title={title}>
      {content}
    </DetailScreen>
  )
}

const today = { to: '/', label: 'Today' }
const calendar = { to: '/calendar', label: 'Calendar' }
const pokedexBack = { to: '/pokedex', label: 'Pokédex' }
const moves = { to: '/pokedex/moves', label: 'Moves' }
const raids = { to: '/battle/raids', label: 'Raids' }
const collection = { to: '/collection', label: 'Collection' }
const settings = { to: '/settings', label: 'Settings' }

function screen(path: string, element: ReactNode, handle: ScreenHandle): RouteObject {
  return { path, element, handle }
}

/** The app's routes. `/design` (the component gallery) exists only on QA and local copies. */
export function appRoutes(environment: AppEnvironment): RouteObject[] {
  const children: RouteObject[] = [
    // A. Today
    { index: true, element: <TodayScreen />, handle: {} satisfies ScreenHandle },
    screen(
      'calendar',
      detail(
        today,
        'Calendar',
        soon({
          screen: 'A2',
          name: 'Calendar',
          feature: 'E06-F02 Calendar',
          text: 'Raid Hours, Community Days, new raids and events, day by day.',
        }),
      ),
      { title: 'Calendar' },
    ),
    screen(
      'calendar/:event',
      detail(
        calendar,
        'Event',
        soon({
          screen: 'A3',
          name: 'Event details',
          feature: 'E06-F03 Event details',
          text: 'What the event brings, its bonuses and your copies of the featured Pokémon.',
        }),
      ),
      { title: 'Event' },
    ),
    screen(
      'eggs',
      detail(
        today,
        'Eggs & research',
        soon({
          screen: 'A4',
          name: 'Eggs & research',
          feature: 'E06-F04 Eggs & research',
          text: 'What hatches from each egg and what research gives, with hundo CPs.',
        }),
      ),
      { title: 'Eggs & research' },
    ),

    // B. Pokédex
    screen(
      'pokedex',
      tab(
        { title: 'Pokédex', sections: pokedex, search: true },
        soon({
          screen: 'B1',
          name: 'Pokédex',
          feature: 'E03-F01 Pokédex list',
          text: 'Every Pokémon with its stats and versions, filtered and sorted your way.',
        }),
      ),
      { title: 'Pokédex' },
    ),
    screen(
      'pokedex/moves',
      tab(
        { title: 'Pokédex', desktopTitle: 'Moves', sections: pokedex },
        soon({
          screen: 'B3',
          name: 'Moves',
          feature: 'E03-F03 Moves list',
          text: 'Every fast and charged move with its raid and PvP numbers.',
        }),
      ),
      { title: 'Moves' },
    ),
    screen(
      'pokedex/moves/:move',
      detail(
        moves,
        undefined,
        soon({
          screen: 'B4',
          name: 'Move',
          feature: 'E03-F04 Move page',
          text: 'A move’s numbers and the Pokémon that learn it.',
        }),
      ),
      { title: 'Move' },
    ),
    screen(
      'pokedex/types',
      tab(
        { title: 'Pokédex', desktopTitle: 'Type chart', sections: pokedex },
        soon({
          screen: 'B5',
          name: 'Type chart',
          feature: 'E03-F05 Type chart',
          text: 'What each type is strong and weak against, with Pokémon GO’s multipliers.',
        }),
      ),
      { title: 'Type chart' },
    ),
    screen(
      'pokedex/:pokemon',
      detail(
        pokedexBack,
        undefined,
        soon({
          screen: 'B2',
          name: 'Pokémon',
          feature: 'E03-F02 Pokémon page',
          text: 'Stats, moves, IVs, where to get it and every version.',
        }),
      ),
      { title: 'Pokémon' },
    ),

    // C. Battle
    { path: 'battle', element: <Navigate to="/battle/raids" replace /> },
    screen(
      'battle/raids',
      tab(
        { title: 'Battle', sections: battle },
        soon({
          screen: 'C1',
          name: 'Raid bosses',
          feature: 'E08-F01 Raid bosses',
          text: 'Today’s raid bosses and what’s coming, with weaknesses and 100% CPs.',
        }),
      ),
      { title: 'Raids' },
    ),
    screen(
      'battle/raids/:boss',
      detail(
        raids,
        undefined,
        soon({
          screen: 'C2',
          name: 'Raid boss',
          feature: 'E08-F02 Raid boss page',
          text: 'The best counters, yours, the boss’s moves and the catch-screen IV check.',
        }),
      ),
      { title: 'Raid boss' },
    ),
    { path: 'battle/tiers', element: <Navigate to="/battle/tiers/great-league" replace /> },
    ...['battle/tiers/raids/:type?', 'battle/tiers/:league'].map((path) =>
      screen(
        path,
        tab(
          { title: 'Battle', sections: battle },
          soon({
            screen: 'C3',
            name: 'Tier lists',
            feature: 'E08-F03 Tier lists',
            text: 'PvP leagues from PvPoke and raid attackers per type, in tiers.',
          }),
        ),
        { title: 'Tier lists' },
      ),
    ),
    screen(
      'battle/teams',
      tab(
        { title: 'Battle', sections: battle },
        soon({
          screen: 'C4',
          name: 'Best team',
          feature: 'E08-F04 Best team',
          text: 'The best six by type or against a boss, from all Pokémon or from yours.',
        }),
      ),
      { title: 'Teams' },
    ),

    // D. Filters
    screen(
      'filters',
      tab(
        { title: 'Filters', sections: filters },
        soon({
          screen: 'D1',
          name: 'Keeper filters',
          feature: 'E04-F01 Keeper filters',
          text: 'Search strings to copy into the game that find the Pokémon worth keeping.',
        }),
      ),
      { title: 'Filters' },
    ),
    screen(
      'filters/builder',
      tab(
        { title: 'Filters', sections: filters },
        soon({
          screen: 'D2',
          name: 'Filter builder',
          feature: 'E04-F04 Filter builder',
          text: 'Build your own search string, checked against the game’s rules.',
        }),
      ),
      { title: 'Filter builder' },
    ),
    screen(
      'filters/saved',
      tab(
        { title: 'Filters', sections: filters },
        <SignedInOnly feature="savedFilters">
          {soon({
            screen: 'D3',
            name: 'Saved filters',
            feature: 'E04-F05 Saved filters',
            text: 'The search strings you saved, on every device.',
          })}
        </SignedInOnly>,
      ),
      { title: 'Saved filters' },
    ),

    // E. Collection (signed in only)
    screen(
      'collection',
      tab(
        { title: 'Collection' },
        <SignedInOnly feature="collection">
          {soon({
            screen: 'E1',
            name: 'My collection',
            feature: 'E07-F03 Collection list',
            text: 'Your Pokémon from Poke Genie, ranked for every league and raid.',
          })}
        </SignedInOnly>,
      ),
      { title: 'Collection' },
    ),
    screen(
      'collection/upload',
      detail(
        collection,
        'Upload',
        <SignedInOnly feature="collection">
          {soon({
            screen: 'E2',
            name: 'Upload',
            feature: 'E07-F01 Poke Genie import',
            text: 'Upload your Poke Genie export to bring your collection in.',
          })}
        </SignedInOnly>,
      ),
      { title: 'Upload' },
    ),
    screen(
      'collection/imports/:id',
      detail(
        collection,
        'Import report',
        <SignedInOnly feature="collection">
          {soon({
            screen: 'E2',
            name: 'Import report',
            feature: 'E07-F02 Re-upload and what changed',
            text: 'What was imported, what wasn’t recognised and what changed.',
          })}
        </SignedInOnly>,
      ),
      { title: 'Import report' },
    ),
    {
      path: 'collection/imports/:id/changes',
      handle: { title: 'All changes' } satisfies ScreenHandle,
      Component: AllChangesScreen,
    },

    // F. Me
    {
      path: 'sign-in',
      element: <SignInRoute />,
      handle: { title: 'Sign in' } satisfies ScreenHandle,
    },
    screen('settings', <SettingsScreen />, { title: 'Settings', hideTabBar: true }),

    // G. Admin (admins only; everyone else gets "Page not found")
    screen(
      'admin',
      detail(
        settings,
        'Admin',
        <AdminOnly>
          {soon({
            screen: 'G1',
            name: 'Data status',
            feature: 'E10-F01 Data status and re-run',
            text: 'Every scheduled job with its last run, Run now and the AI spend.',
          })}
        </AdminOnly>,
      ),
      { title: 'Admin', hideTabBar: true },
    ),
    screen(
      'admin/entries',
      detail(
        settings,
        'Admin',
        <AdminOnly>
          {soon({
            screen: 'G2',
            name: 'Fix fetched entries',
            feature: 'E10-F02 Fix fetched entries',
            text: 'Fix or remove what the AI researcher fetched; fixed entries stay locked.',
          })}
        </AdminOnly>,
      ),
      { title: 'Admin', hideTabBar: true },
    ),

    // Google sends people back here after signing in.
    screen('auth/callback', <CallbackRoute />, { title: 'Signing in', hideTabBar: true }),
  ]

  if (environment.kind !== 'production') {
    children.push(screen('design', <DesignScreen />, { title: 'Design system' }))
  }

  children.push(screen('*', <NotFoundScreen />, { title: 'Page not found' }))

  return [
    {
      path: '/',
      element: <AppShell environment={environment} />,
      errorElement: <RouteError />,
      children,
    },
  ]
}
