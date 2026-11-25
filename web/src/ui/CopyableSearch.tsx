import { useState } from 'react'
import { useTranslation } from 'react-i18next'

export function CopyableSearch({
  searchString,
  range,
}: {
  searchString: string
  range?: { number: string; dex: string }
}) {
  const { t } = useTranslation()
  const [status, setStatus] = useState<'idle' | 'copied' | 'manual'>('idle')
  const action = t(status === 'copied' ? 'keepers.copied' : 'keepers.copy')

  async function copy() {
    try {
      await navigator.clipboard.writeText(searchString)
      setStatus('copied')
    } catch {
      setStatus('manual')
    }
  }

  return (
    <div className="keeper-part">
      {range && (
        <span className="keeper-range">
          {range.number} <span>{range.dex}</span>
        </span>
      )}
      <code className="keeper-preview">{searchString}</code>
      <button
        type="button"
        className="button keeper-copy"
        data-copied={status === 'copied' || undefined}
        aria-label={
          range ? t('keepers.copyPart', { action, part: range.number, range: range.dex }) : action
        }
        onClick={() => void copy()}
      >
        <span aria-live="polite">{action}</span>
        {range && (
          <span className="keeper-copy-range">
            {range.number} · {range.dex}
          </span>
        )}
      </button>
      {status === 'manual' && (
        <label className="keeper-manual" role="status">
          {t('keepers.manualCopy')}
          <textarea
            readOnly
            rows={3}
            spellCheck={false}
            value={searchString}
            onFocus={(event) => event.currentTarget.select()}
          />
        </label>
      )}
    </div>
  )
}
