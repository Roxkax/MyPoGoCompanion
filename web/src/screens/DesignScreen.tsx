import { useState } from 'react'
import { DetailScreen } from '../layout/Screen'
import { Card } from '../ui/Card'
import { Chip } from '../ui/Chip'
import { Pill } from '../ui/Pill'
import { SegmentedSwitch } from '../ui/SegmentedSwitch'
import { SpriteBox } from '../ui/SpriteBox'
import { TypeBadge } from '../ui/TypeBadge'
import { VersionMarker } from '../ui/VersionMarker'
import { pokemonTypes, versionKinds } from '../ui/pokemonTypes'

/**
 * /design, on QA and local copies only: every shared component in one place, to check the design system in light
 * and dark before the screens that use them exist.
 */
export function DesignScreen() {
  const [kind, setKind] = useState<'fast' | 'charged'>('charged')
  const [shiny, setShiny] = useState(true)
  const [type, setType] = useState<string | null>('Dragon')

  return (
    <DetailScreen back={{ to: '/', label: 'Today' }} title="Design system">
      <div className="stack">
        <Card as="section" aria-labelledby="design-types">
          <h2 id="design-types" className="group-label">
            Type badges
          </h2>
          <div className="wrap-row">
            {pokemonTypes.map((t) => (
              <TypeBadge key={t} type={t} />
            ))}
          </div>
          <div className="wrap-row">
            <TypeBadge type="dragon" size="large" />
            <TypeBadge type="ground" size="large" />
          </div>
        </Card>
        <Card as="section" aria-labelledby="design-versions">
          <h2 id="design-versions" className="group-label">
            Version markers
          </h2>
          <div className="wrap-row">
            {versionKinds.map((k) => (
              <VersionMarker key={k} kind={k} />
            ))}
          </div>
        </Card>
        <Card as="section" aria-labelledby="design-pills">
          <h2 id="design-pills" className="group-label">
            Pills
          </h2>
          <div className="wrap-row">
            <Pill tone="live" dot>
              Live now
            </Pill>
            <Pill>Ends Sat 31 Oct 20:00</Pill>
            <Pill tone="accent">Admin</Pill>
            <Pill tone="warn">Out of date</Pill>
            <Pill tone="danger">Gone</Pill>
            <Pill tone="gold">Lucky</Pill>
            <Pill tone="raid">Raid 97%</Pill>
          </div>
        </Card>
        <Card as="section" aria-labelledby="design-chips">
          <h2 id="design-chips" className="group-label">
            Chips and switches
          </h2>
          <div className="wrap-row">
            {type && (
              <Chip onRemove={() => setType(null)} removeLabel={`Remove ${type}`}>
                {type}
              </Chip>
            )}
            <Chip selected={shiny} onClick={() => setShiny((on) => !on)}>
              Shiny
            </Chip>
            <Chip>Released</Chip>
          </div>
          <SegmentedSwitch
            label="Move kind"
            value={kind}
            onChange={setKind}
            options={[
              { value: 'fast', label: 'Fast', count: 80 },
              { value: 'charged', label: 'Charged', count: 235 },
            ]}
          />
        </Card>
        <Card as="section" aria-labelledby="design-sprites">
          <h2 id="design-sprites" className="group-label">
            Sprite boxes
          </h2>
          <div className="wrap-row">
            <SpriteBox src="/icon.svg" alt="App icon" size={64} />
            <SpriteBox alt="No picture yet" size={64} />
            <SpriteBox src="/missing-sprite.png" alt="Picture that fails to load" size={40} />
          </div>
        </Card>
        <Card padded={false} as="section" aria-labelledby="design-card">
          <h2 id="design-card" className="group-label card-rows-title">
            Card with rows
          </h2>
          <ul className="card-rows">
            <li>Great League</li>
            <li>Ultra League</li>
            <li>Master League</li>
          </ul>
        </Card>
      </div>
    </DetailScreen>
  )
}
