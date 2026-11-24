import { useTranslation } from 'react-i18next'
import { Card } from '../../ui/Card'
import type { PokemonType } from '../../ui/pokemonTypes'
import { chartTypes, defendingPower, matchupPower } from './chart'
import { RelatedMoves, RelatedPokemon } from './RelatedTypes'
import { useMultiplier } from './useMultiplier'

type Selection =
  | { attacker: PokemonType; defenders?: never }
  | {
      attacker?: never
      defenders: readonly PokemonType[]
    }

export function MatchupResults({ attacker, defenders }: Selection) {
  const { t } = useTranslation()
  const format = useMultiplier()
  const all = chartTypes.map((type) => ({
    type,
    power: attacker ? matchupPower(attacker, type) : defendingPower(type, defenders),
  }))
  const groups = attacker
    ? ([
        { label: 'strong', power: 1, rows: all.filter((row) => row.power === 1) },
        { label: 'weak', power: -1, rows: all.filter((row) => row.power === -1) },
        { label: 'barely', power: -2, rows: all.filter((row) => row.power === -2) },
      ] as const)
    : ([
        {
          label: 'weakTo',
          power: null,
          rows: all.filter((row) => row.power > 0).sort((a, b) => b.power - a.power),
        },
        {
          label: 'resists',
          power: null,
          rows: all.filter((row) => row.power < 0).sort((a, b) => a.power - b.power),
        },
        { label: 'neutral', power: 0, rows: all.filter((row) => row.power === 0) },
      ] as const)
  return (
    <Card className="type-chart-results" padded={false}>
      {groups.map(({ label, power, rows }) => (
        <section className="type-chart-result" key={label}>
          <div className="type-chart-result-head">
            <h2>{t(`chart.${label}`)}</h2>
            {power !== null && <span>{format(power)}</span>}
          </div>
          {rows.length > 0 ? (
            <div className="type-chart-chips">
              {rows.map((row) => (
                <span key={row.type} className="ui-type" data-type={row.type}>
                  {t(`types.${row.type}`)}
                  {power === null && ` ${format(row.power)}`}
                </span>
              ))}
            </div>
          ) : (
            <p>{t('chart.none')}</p>
          )}
        </section>
      ))}
      {attacker ? <RelatedMoves type={attacker} /> : <RelatedPokemon types={defenders} />}
    </Card>
  )
}
