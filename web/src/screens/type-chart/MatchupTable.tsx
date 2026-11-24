import { useRef, useState, type KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Sheet } from '../../ui/Sheet'
import type { PokemonType } from '../../ui/pokemonTypes'
import { Card } from '../../ui/Card'
import { chartTypes, matchupPower } from './chart'
import { useMultiplier } from './useMultiplier'

const legend = [
  { power: 1, label: 'legendStrong' },
  { power: -1, label: 'legendWeak' },
  { power: -2, label: 'legendBarely' },
  { power: 0, label: 'neutral' },
] as const

export function MatchupTable({
  attacker,
  defenders,
}: {
  attacker: PokemonType | null
  defenders: readonly PokemonType[]
}) {
  const { t } = useTranslation()
  const format = useMultiplier()
  const [detail, setDetail] = useState<string | null>(null)
  const [focused, setFocused] = useState(0)
  const buttons = useRef<(HTMLButtonElement | null)[]>([])
  const size = chartTypes.length

  function moveFocus(event: KeyboardEvent, row: number, column: number) {
    const delta = {
      ArrowDown: [1, 0],
      ArrowUp: [-1, 0],
      ArrowLeft: [0, -1],
      ArrowRight: [0, 1],
    }[event.key]
    if (!delta) return

    event.preventDefault()
    const nextRow = Math.max(0, Math.min(size - 1, row + delta[0]!))
    const nextColumn = Math.max(0, Math.min(size - 1, column + delta[1]!))
    buttons.current[nextRow * size + nextColumn]?.focus()
  }

  function header(type: PokemonType, scope: 'row' | 'col', selected: boolean) {
    const name = t(`types.${type}`)
    return (
      <th
        key={type}
        scope={scope}
        data-highlight={selected || undefined}
        title={name}
        aria-label={name}
      >
        <span className="ui-type" data-type={type}>
          {name.slice(0, 3)}
        </span>
      </th>
    )
  }

  return (
    <Card className="type-chart-matrix">
      <ul className="type-chart-legend">
        {legend.map(({ power, label }) => (
          <li key={power}>
            <span data-power={power} aria-hidden />
            {format(power)} {t(`chart.${label}`)}
          </li>
        ))}
      </ul>
      <div className="type-chart-scroll">
        <table aria-label={t('chart.full')}>
          <thead>
            <tr>
              <th className="type-chart-corner">
                {t('chart.attackShort')} ↓<br />
                {t('chart.defenseShort')} →
              </th>
              {chartTypes.map((type) => header(type, 'col', defenders.includes(type)))}
            </tr>
          </thead>
          <tbody>
            {chartTypes.map((attack, row) => (
              <tr key={attack}>
                {header(attack, 'row', attack === attacker)}
                {chartTypes.map((defense, column) => {
                  const power = matchupPower(attack, defense)
                  const description = t('chart.matchup', {
                    attacking: t(`types.${attack}`),
                    defending: t(`types.${defense}`),
                    multiplier: format(power),
                  })
                  const index = row * size + column
                  return (
                    <td
                      key={defense}
                      data-highlight={
                        attack === attacker || defenders.includes(defense) || undefined
                      }
                    >
                      <button
                        ref={(element) => {
                          buttons.current[index] = element
                        }}
                        type="button"
                        data-power={power}
                        tabIndex={focused === index ? 0 : -1}
                        aria-label={description}
                        onFocus={() => setFocused(index)}
                        onKeyDown={(event) => moveFocus(event, row, column)}
                        onClick={() => setDetail(description)}
                      >
                        {power !== 0 && format(power).slice(1)}
                      </button>
                    </td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p>{t('chart.axes')}</p>
      {detail && (
        <Sheet id="type-matchup" title={t('chart.detail')} onClose={() => setDetail(null)}>
          <p>{detail}</p>
        </Sheet>
      )}
    </Card>
  )
}
