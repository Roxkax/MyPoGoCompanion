import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { $api, api, dataOrThrow, type Schemas } from '../../api/client'
import { useCurrentLanguage, useFormats } from '../../i18n/languageContext'
import { useIsDesktop } from '../../layout/useMediaQuery'
import { CopyableSearch } from '../../ui/CopyableSearch'
import { isPokemonType } from '../../ui/pokemonTypes'
import { Sheet } from '../../ui/Sheet'
import { ConditionEditor, ConditionPicker } from './ConditionPicker'
import {
  characterLimit,
  formatConditions,
  inputLimit,
  keywords,
  readConditions,
  readRange,
  type Condition,
} from './filterBuilder'

type Editing = { index?: number; condition: Condition }
type Check = Schemas['SearchCheckResult']
type Completed = { expression: string; result: Check | 'error'; attempt: number }
type Keyword = (typeof keywords)[keyof typeof keywords][number]
const translatedKeywords: ReadonlySet<string> = new Set(Object.values(keywords).flat())

function useSearchCheck(expression: string) {
  const [completed, setCompleted] = useState<Completed | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    if (!expression.trim() || expression.length > inputLimit) return
    const controller = new AbortController()
    const timer = window.setTimeout(() => {
      void api
        .POST('/filter-search/check', { body: { expression }, signal: controller.signal })
        .then((response) => dataOrThrow(response))
        .then((result) => {
          if (!controller.signal.aborted) setCompleted({ expression, attempt, result })
        })
        .catch(() => {
          if (!controller.signal.aborted) setCompleted({ expression, attempt, result: 'error' })
        })
    }, 275)
    return () => {
      window.clearTimeout(timer)
      controller.abort()
    }
  }, [expression, attempt])

  const result =
    completed?.expression === expression && completed.attempt === attempt ? completed.result : null
  return { result, retry: () => setAttempt((value) => value + 1) }
}

export function FilterBuilderScreen() {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const formats = useFormats()
  const desktop = useIsDesktop()
  const [expression, setExpression] = useState('')
  const [pickerOpen, setPickerOpen] = useState(false)
  const [editing, setEditing] = useState<Editing | null>(null)
  const { result, retry } = useSearchCheck(expression)
  const catalog = $api.useQuery('get', '/pokedex', undefined, { staleTime: 60 * 60 * 1000 })

  const { names, labels } = useMemo(() => {
    const names = new Set<string>()
    const labels = new Map<string, string>()
    for (const { dex, nameEn, namePt } of catalog.data?.species ?? []) {
      const key = nameEn.toLowerCase()
      const label = language === 'pt-PT' ? (namePt ?? nameEn) : nameEn
      names.add(key)
      labels.set(key, label)
      labels.set(String(dex), label)
    }
    for (const { nameEn, namePt } of catalog.data?.moves ?? []) {
      labels.set(`@${nameEn.toLowerCase()}`, language === 'pt-PT' ? (namePt ?? nameEn) : nameEn)
    }
    return { names, labels }
  }, [catalog.data, language])

  const conditions = useMemo(() => readConditions(expression, names), [expression, names])

  function describeTerm(body: string) {
    const name = body.startsWith('+') ? labels.get(body.slice(1)) : null
    if (name) return t('filterBuilder.familyOf', { name })
    const label = labels.get(body)
    if (label) return label
    if (isPokemonType(body)) return t(`types.${body}`)
    if (translatedKeywords.has(body)) return t(`filterBuilder.keywords.${body as Keyword}`)
    const range = readRange(body)
    if (!range) return null
    const { kind, low, high } = range
    const field = t(`filterBuilder.fields.${kind}`)
    if (low === null && high !== null)
      return t('filterBuilder.rangeAtMost', { field, value: formats.number(high) })
    if (low !== null && high === null)
      return t('filterBuilder.rangeAtLeast', { field, value: formats.number(low) })
    if (low !== null && high !== null) {
      if (low === high) return t('filterBuilder.rangeExact', { field, value: formats.number(low) })
      return t('filterBuilder.rangeBetween', {
        field,
        low: formats.number(low),
        high: formats.number(high),
      })
    }
    return null
  }

  function labelTerm(term: string) {
    const body = term.replace(/^!/, '')
    const label = describeTerm(body.toLowerCase()) ?? body
    return term.startsWith('!') ? t('filterBuilder.not', { value: label }) : label
  }

  function closePicker() {
    setPickerOpen(false)
    setEditing(null)
  }

  function apply(terms: string[]) {
    if (!conditions || !editing) return
    const next = [...conditions]
    next.splice(editing.index ?? next.length, editing.index == null ? 0 : 1, {
      group: editing.condition.group,
      terms,
    })
    setExpression(formatConditions(next))
    closePicker()
  }

  const picker = editing ? (
    <ConditionEditor
      key={`${editing.condition.group}:${editing.index ?? 'new'}`}
      condition={editing.condition}
      catalog={catalog.data}
      catalogStatus={catalog.status}
      retryCatalog={() => void catalog.refetch()}
      labelTerm={labelTerm}
      onCancel={closePicker}
      onApply={apply}
    />
  ) : (
    <ConditionPicker onSelect={(group) => setEditing({ condition: { group, terms: [] } })} />
  )
  const pickerTitle = editing
    ? editing.condition.group === 'custom'
      ? t('filterBuilder.custom')
      : t(`filterBuilder.groups.${editing.condition.group}.title`)
    : t('filterBuilder.addCondition')

  const status =
    expression.length > inputLimit
      ? 'TooLong'
      : !expression.trim()
        ? 'Empty'
        : result === 'error'
          ? 'Error'
          : (result?.status ?? 'Checking')
  const checkedSearch = result && result !== 'error' ? result : null

  return (
    <section className="filter-builder" aria-label={t('titles.filterBuilder')}>
      {desktop && (
        <section
          className="ui-card ui-card-padded builder-card"
          aria-labelledby="builder-picker-title"
        >
          <h2 id="builder-picker-title">{pickerTitle}</h2>
          {conditions ? picker : <p>{t('filterBuilder.fixBeforeEditing')}</p>}
        </section>
      )}
      <section
        className="ui-card ui-card-padded builder-card"
        aria-labelledby="builder-conditions-title"
      >
        <div className="builder-card-header">
          <h2 id="builder-conditions-title">{t('filterBuilder.conditions')}</h2>
          <button
            className="builder-link"
            type="button"
            disabled={!expression}
            onClick={() => {
              setExpression('')
              closePicker()
            }}
          >
            {t('filterBuilder.clear')}
          </button>
        </div>
        {!conditions ? (
          <p className="builder-note">{t('filterBuilder.fixBeforeEditing')}</p>
        ) : conditions.length === 0 ? (
          <p className="builder-note">{t('filterBuilder.noConditions')}</p>
        ) : (
          <ol className="builder-conditions">
            {conditions.map((condition, index) => {
              const title =
                condition.group === 'custom'
                  ? t('filterBuilder.custom')
                  : t(`filterBuilder.groups.${condition.group}.title`)
              const values = condition.terms.map(labelTerm).join(` ${t('filterBuilder.or')} `)
              return (
                <li key={index}>
                  {index > 0 && <span className="builder-join">{t('filterBuilder.and')}</span>}
                  <div className="builder-row">
                    <button
                      type="button"
                      className="builder-condition"
                      aria-label={t('filterBuilder.editCondition', { title, values })}
                      onClick={() => setEditing({ index, condition })}
                    >
                      <span>{title}</span>
                      <strong>{values}</strong>
                    </button>
                    <button
                      type="button"
                      className="icon-button"
                      aria-label={t('filterBuilder.removeCondition', { title, values })}
                      onClick={() => {
                        setExpression(formatConditions(conditions.filter((_, at) => at !== index)))
                        closePicker()
                      }}
                    >
                      <span aria-hidden="true">×</span>
                    </button>
                  </div>
                </li>
              )
            })}
          </ol>
        )}
        {!desktop && (
          <button
            className="builder-add"
            type="button"
            disabled={!conditions}
            onClick={() => setPickerOpen(true)}
          >
            <span aria-hidden="true">＋</span> {t('filterBuilder.addCondition')}
          </button>
        )}
      </section>

      <section className="ui-card ui-card-padded builder-card">
        <label className="builder-field" htmlFor="builder-expression">
          <strong>{t('filterBuilder.searchString')}</strong>
          <span className="builder-note">{t('filterBuilder.editByHand')}</span>
        </label>
        <textarea
          id="builder-expression"
          rows={4}
          dir="ltr"
          spellCheck={false}
          autoCapitalize="none"
          autoComplete="off"
          value={expression}
          onChange={(event) => {
            setExpression(event.target.value)
            closePicker()
          }}
        />
        <div className="builder-meta">
          <span role="status" data-status={status}>
            {t(`filterBuilder.status.${status}`, { term: checkedSearch?.term || '' })}
            {status === 'Error' && (
              <>
                {' '}
                <button className="builder-link" type="button" onClick={retry}>
                  {t('filterBuilder.retry')}
                </button>
              </>
            )}
          </span>
          <span aria-label={t('filterBuilder.length')}>
            {formats.number(expression.length)} / {formats.number(characterLimit)}
          </span>
        </div>
        {checkedSearch && checkedSearch.parts.length > 0 && (
          <div className="builder-copy">
            {checkedSearch.parts.length > 1 && (
              <p className="builder-note">{t('filterBuilder.pasteParts')}</p>
            )}
            {checkedSearch.parts.map((part, index) => (
              <CopyableSearch
                key={`${index}:${part}`}
                searchString={part}
                preview={checkedSearch.parts.length > 1}
                range={
                  checkedSearch.parts.length > 1
                    ? { number: t('filterBuilder.part', { number: formats.number(index + 1) }) }
                    : undefined
                }
              />
            ))}
          </div>
        )}
      </section>

      {!desktop && (pickerOpen || editing) && (
        <Sheet id="condition-picker" title={pickerTitle} onClose={closePicker}>
          {picker}
        </Sheet>
      )}
    </section>
  )
}
