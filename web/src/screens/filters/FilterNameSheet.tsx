import { useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Sheet } from '../../ui/Sheet'

export function FilterNameSheet({
  title,
  search,
  initialName = '',
  action,
  onClose,
  onSubmit,
}: {
  title: string
  search: string
  initialName?: string
  action: string
  onClose: () => void
  onSubmit: (name: string) => Promise<void>
}) {
  const { t } = useTranslation()
  const id = useId()
  const [name, setName] = useState(initialName)
  const [pending, setPending] = useState(false)
  const [failed, setFailed] = useState(false)
  const submitting = useRef(false)

  async function submit() {
    if (submitting.current || !name.trim()) return
    submitting.current = true
    setPending(true)
    setFailed(false)
    try {
      await onSubmit(name.trim())
    } catch {
      setFailed(true)
    } finally {
      submitting.current = false
      setPending(false)
    }
  }

  return (
    <Sheet
      id={id}
      title={title}
      onClose={() => {
        if (!submitting.current) onClose()
      }}
    >
      <form
        className="saved-filter-form"
        onSubmit={(event) => {
          event.preventDefault()
          void submit()
        }}
      >
        <label htmlFor={`${id}-name`}>{t('savedFilters.name')}</label>
        <input
          id={`${id}-name`}
          value={name}
          maxLength={100}
          required
          disabled={pending}
          onChange={(event) => setName(event.target.value)}
        />
        <code dir="ltr">{search}</code>
        {failed && <p role="alert">{t('savedFilters.actionError')}</p>}
        <div className="saved-filter-actions">
          <button type="button" className="button" disabled={pending} onClick={onClose}>
            {t('savedFilters.cancel')}
          </button>
          <button type="submit" className="button" disabled={pending || !name.trim()}>
            {action}
          </button>
        </div>
      </form>
    </Sheet>
  )
}
