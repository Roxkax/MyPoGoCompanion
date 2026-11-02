import { useTranslation } from 'react-i18next'

/** "Reference 7f3a2c91" under an error: what to quote to find it in Application Insights. Nothing without one. */
export function ErrorReference({ reference }: { reference: string | null | undefined }) {
  const { t } = useTranslation()
  if (!reference) {
    return null
  }
  return (
    <p className="error-reference">
      {t('errors.reference')} <code>{reference}</code>
    </p>
  )
}
