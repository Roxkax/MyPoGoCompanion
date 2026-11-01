import { NavLink } from 'react-router'

export interface SegmentLink {
  to: string
  label: string
  /** Only this exact address counts as this section (not the addresses below it). */
  end?: boolean
}

/** The section switcher under a tab's title (Pokémon · Moves · Type chart, Raids · Tier lists · Teams…): links. */
export function SegmentedLinks({ label, links }: { label: string; links: readonly SegmentLink[] }) {
  return (
    <nav className="ui-seg" aria-label={label}>
      {links.map((link) => (
        <NavLink key={link.to} to={link.to} end={link.end} className="ui-seg-option">
          {link.label}
        </NavLink>
      ))}
    </nav>
  )
}
