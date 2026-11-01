import { describe, expect, it } from 'vitest'
import { activeSidebarItem, activeTab } from './nav'

describe('activeTab', () => {
  it.each([
    ['/', 'today'],
    ['/calendar', 'today'],
    ['/calendar/community-day-gible-2026-11-07', 'today'],
    ['/eggs', 'today'],
    ['/pokedex', 'pokedex'],
    ['/pokedex/garchomp', 'pokedex'],
    ['/pokedex/moves/earth-power', 'pokedex'],
    ['/battle/raids/kyogre', 'battle'],
    ['/battle/tiers/great-league', 'battle'],
    ['/filters/saved', 'filters'],
    ['/collection/imports/42/changes', 'collection'],
    ['/settings', null],
    ['/admin', null],
    ['/sign-in', null],
    ['/no/such/page', null],
  ])('%s → %s', (path, tab) => {
    expect(activeTab(path)).toBe(tab)
  })
})

describe('activeSidebarItem', () => {
  it.each([
    ['/', 'today'],
    ['/calendar', 'calendar'],
    ['/calendar/community-day-gible-2026-11-07', 'calendar'],
    ['/eggs', 'eggs'],
    ['/pokedex', 'pokedex'],
    ['/pokedex/garchomp', 'pokedex'],
    ['/pokedex/types', 'pokedex'],
    ['/pokedex/moves', 'moves'],
    ['/pokedex/moves/earth-power', 'moves'],
    ['/battle/raids', 'battle'],
    ['/battle/teams', 'battle'],
    ['/battle/tiers/raids/ground', 'tiers'],
    ['/filters/builder', 'filters'],
    ['/collection', 'collection'],
    ['/settings', 'settings'],
    ['/admin/entries', 'admin'],
    ['/no/such/page', null],
  ])('%s → %s', (path, item) => {
    expect(activeSidebarItem(path)).toBe(item)
  })
})
