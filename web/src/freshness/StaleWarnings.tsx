import { useTranslation } from 'react-i18next'
import { Icon } from '../components/icons'
import { staleKinds, type DataKind } from './freshness'
import { useFreshness } from './useFreshness'

/**
 * The orange out-of-date bars (G1's "What users see if updates stop"), one per kind of data on this screen that has
 * stopped updating. Nothing at all while the data is up to date, or before the API has answered.
 */
export function StaleWarnings({ kinds }: { kinds: readonly DataKind[] }) {
  const stale = staleKinds(useFreshness(), kinds)
  if (stale.length === 0) {
    return null
  }
  return (
    <div className="stale-warnings">
      {stale.map(({ kind, days }) => (
        <StaleBar key={kind} kind={kind} days={days} />
      ))}
    </div>
  )
}

/** One bar: "Raids and events haven't updated in 12 days. Some may have changed in the game." */
export function StaleBar({ kind, days }: { kind: DataKind; days: number }) {
  const { t } = useTranslation()
  return (
    <p className="stale-warning" data-kind={kind}>
      <Icon name="warning" size={18} strokeWidth={2.4} />
      <span>{t(kind === 'game' ? 'stale.game' : 'stale.live', { count: days })}</span>
    </p>
  )
}
