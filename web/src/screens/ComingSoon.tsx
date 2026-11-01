import { useParams } from 'react-router'
import { Card } from '../ui/Card'
import { Pill } from '../ui/Pill'

/**
 * Stands in for a screen that isn't built yet, inside the real shell (decision 2026-11-01): says what the screen
 * will be and which feature builds it. Each feature replaces its placeholder.
 */
export function ComingSoon({
  screen,
  name,
  feature,
  text,
}: {
  /** The screen's code in the design (e.g. "B2"). */
  screen: string
  name: string
  /** The backlog feature that builds it (e.g. "E03-F02 Pokémon page"). */
  feature: string
  /** One line about what the screen will do. */
  text: string
}) {
  const params = Object.values(useParams()).filter((value): value is string => !!value)
  return (
    <Card as="section" className="coming-soon" aria-labelledby={`coming-soon-${screen}`}>
      <Pill tone="accent">Coming soon</Pill>
      <h2 id={`coming-soon-${screen}`}>{name}</h2>
      {params.length > 0 && <p className="coming-soon-params">{params.join(' · ')}</p>}
      <p>{text}</p>
      <p className="fine-print">
        Screen {screen} · comes with {feature}
      </p>
    </Card>
  )
}
