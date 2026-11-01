import type { ReactNode } from 'react'

export type PillTone = 'live' | 'soft' | 'accent' | 'warn' | 'danger' | 'gold' | 'raid'

/** A small label: "Live now", "Admin", "Ends Sat 31 Oct", "Lucky", "Raid 97%"… */
export function Pill({
  tone = 'soft',
  dot = false,
  children,
}: {
  tone?: PillTone
  /** A dot before the text, for live things. */
  dot?: boolean
  children: ReactNode
}) {
  return (
    <span className="ui-pill" data-tone={tone}>
      {dot && <span className="ui-pill-dot" aria-hidden="true" />}
      {children}
    </span>
  )
}
