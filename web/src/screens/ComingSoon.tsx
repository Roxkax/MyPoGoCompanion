import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router'
import type { SoonKey } from '../i18n/keys'
import { Card } from '../ui/Card'
import { Pill } from '../ui/Pill'

/**
 * Stands in for a screen that isn't built yet, inside the real shell (decision 2026-11-01): says what the screen
 * will be and which feature builds it. Each feature replaces its placeholder.
 */
export function ComingSoon({
  id,
  screen,
  feature,
}: {
  /** Its texts: soon.<id>.name and soon.<id>.text (one line about what the screen will do). */
  id: SoonKey
  /** The screen's code in the design (e.g. "B2"). */
  screen: string
  /** The backlog feature that builds it (e.g. "E03-F02 Pokémon page"), as the backlog names it. */
  feature: string
}) {
  const { t } = useTranslation()
  const params = Object.values(useParams()).filter((value): value is string => !!value)
  return (
    <Card as="section" className="coming-soon" aria-labelledby={`coming-soon-${screen}`}>
      <Pill tone="accent">{t('soon.badge')}</Pill>
      <h2 id={`coming-soon-${screen}`}>{t(`soon.${id}.name`)}</h2>
      {params.length > 0 && <p className="coming-soon-params">{params.join(' · ')}</p>}
      <p>{t(`soon.${id}.text`)}</p>
      <p className="fine-print">{t('soon.footer', { screen, feature })}</p>
    </Card>
  )
}
