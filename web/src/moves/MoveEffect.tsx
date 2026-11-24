import { useTranslation } from 'react-i18next'
import { useFormats } from '../i18n/languageContext'
import type { Move } from './view'

/** Show all simultaneous stat changes, including their probability; no effect is inferred from a move's name. */
export function MoveEffect({ move }: { move: Move }) {
  const { t } = useTranslation()
  const formats = useFormats()
  const changes = [
    ['self', 'attack', move.pvpSelfAttack],
    ['self', 'defense', move.pvpSelfDefense],
    ['foe', 'attack', move.pvpOpponentAttack],
    ['foe', 'defense', move.pvpOpponentDefense],
  ] as const
  const active = changes.filter(([, , value]) => value !== 0)
  if (!active.length || move.pvpBuffChance === 0) return <span className="moves-no-effect">—</span>
  return (
    <span className="moves-effects">
      {active.map(([who, stat, value]) => (
        <span key={`${who}-${stat}`} data-target={who}>
          {t(`moves.effects.${who}`)} {t(`moves.effects.${stat}`)} {value > 0 ? '+' : '−'}
          {formats.number(Math.abs(value))}
        </span>
      ))}
      <small>
        {move.pvpBuffChance == null
          ? t('moves.effects.unknownChance')
          : t('moves.effects.chance', {
              value: formats.number(move.pvpBuffChance, {
                style: 'percent',
                maximumFractionDigits: 1,
              }),
            })}
      </small>
    </span>
  )
}
