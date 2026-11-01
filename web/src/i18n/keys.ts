import type { en } from './messages/en'

// Keys that screens and settings pass around (a route's title, a nav item's label), checked by the type checker.

export type TitleKey = keyof typeof en.titles
export type NavKey = keyof typeof en.nav
export type SectionKey = keyof typeof en.sections
export type SoonKey = Exclude<keyof typeof en.soon, 'badge' | 'footer'>
