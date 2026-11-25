import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { Schemas } from '../../api/client'
import { useCurrentLanguage } from '../../i18n/languageContext'
import { normalize } from '../../pokedex/normalize'
import { pokemonTypes } from '../../ui/pokemonTypes'
import {
  customTerm,
  groupRanges,
  groups,
  keywords,
  ranges,
  rangeTerm,
  type Condition,
  type Group,
  type RangeKind,
} from './filterBuilder'

type Choice = { value: string; label: string; search?: string }
const noRange: readonly RangeKind[] = []

export function ConditionPicker({ onSelect }: { onSelect: (group: Group) => void }) {
  const { t } = useTranslation()
  const [query, setQuery] = useState('')
  const match = normalize(query)
  const shown = groups.filter((group) =>
    normalize(
      [
        t(`filterBuilder.groups.${group}.title`),
        t(`filterBuilder.groups.${group}.description`),
        ...keywords[group].flatMap((word) => [word, t(`filterBuilder.keywords.${word}`)]),
        ...groupRanges[group].map((range) => t(`filterBuilder.fields.${range}`)),
        ...(group === 'types' ? pokemonTypes.flatMap((type) => [type, t(`types.${type}`)]) : []),
      ].join(' '),
    ).includes(match),
  )

  return (
    <div className="builder-picker">
      <label className="builder-field">
        {t('filterBuilder.findCondition')}
        <input type="search" value={query} onChange={(event) => setQuery(event.target.value)} />
      </label>
      <div className="builder-categories">
        {shown.map((group) => (
          <button type="button" key={group} onClick={() => onSelect(group)}>
            <strong>{t(`filterBuilder.groups.${group}.title`)}</strong>
            <span>{t(`filterBuilder.groups.${group}.description`)}</span>
          </button>
        ))}
      </div>
      {shown.length === 0 && <p role="status">{t('filterBuilder.noMatches')}</p>}
      <p className="builder-note">{t('filterBuilder.logic')}</p>
    </div>
  )
}

export function ConditionEditor({
  condition,
  catalog,
  catalogStatus,
  retryCatalog,
  labelTerm,
  onCancel,
  onApply,
}: {
  condition: Condition
  catalog?: Schemas['PokedexResponse']
  catalogStatus: 'pending' | 'error' | 'success'
  retryCatalog: () => void
  labelTerm: (term: string) => string
  onCancel: () => void
  onApply: (terms: string[]) => void
}) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const [selected, setSelected] = useState(() => [...new Set(condition.terms)])
  const [exclude, setExclude] = useState(false)
  const [family, setFamily] = useState(false)
  const [slot, setSlot] = useState('')
  const [query, setQuery] = useState('')
  const [text, setText] = useState('')
  const { group } = condition
  const rangeKinds = group === 'custom' ? noRange : groupRanges[group]
  const hasCatalog = group === 'pokemon' || group === 'moves'
  const choices: Choice[] =
    group === 'custom'
      ? []
      : keywords[group].map((value) => ({ value, label: t(`filterBuilder.keywords.${value}`) }))
  const listedTypes = group === 'types' || group === 'moves'
  const custom = group === 'tags' || group === 'custom'

  if (listedTypes) {
    choices.push(
      ...pokemonTypes.map((type) => ({
        value: group === 'moves' ? `@${slot}${type}` : type,
        label:
          group === 'moves'
            ? t('filterBuilder.moveType', { type: t(`types.${type}`) })
            : t(`types.${type}`),
      })),
    )
  }

  const catalogChoices: Choice[] =
    group === 'pokemon'
      ? (catalog?.species ?? []).map(({ dex, nameEn, namePt }) => ({
          value: family ? `+${nameEn.toLowerCase()}` : String(dex),
          label: `${language === 'pt-PT' ? (namePt ?? nameEn) : nameEn} · #${dex}`,
          search: nameEn,
        }))
      : group === 'moves' && !slot
        ? (catalog?.moves ?? []).map(({ nameEn, namePt }) => ({
            value: `@${nameEn.toLowerCase()}`,
            label: language === 'pt-PT' ? (namePt ?? nameEn) : nameEn,
            search: nameEn,
          }))
        : []

  function include(value: string) {
    return exclude ? `!${value}` : value
  }

  function toggle(value: string) {
    const term = include(value)
    setSelected((current) =>
      current.includes(term) ? current.filter((item) => item !== term) : [...current, term],
    )
  }

  function add(value: string) {
    const term = include(value)
    setSelected((current) => (current.includes(term) ? current : [...current, term]))
  }

  const find = normalize(query)
  const matches = ({ value, label, search }: Choice) =>
    normalize(`${label} ${value} ${search ?? ''}`).includes(find)
  const staticMatches = choices.filter(matches)
  const catalogMatches = catalogChoices.filter(matches)
  const searchChoices = choices.length > 7 || hasCatalog
  const visible = [...staticMatches, ...catalogMatches.slice(0, 12)]
  const customValue = customTerm(text, group === 'tags' ? '#' : '')

  return (
    <div className="builder-editor">
      <label className="builder-toggle">
        <input
          type="checkbox"
          checked={exclude}
          onChange={(event) => setExclude(event.target.checked)}
        />
        {t('filterBuilder.exclude')}
      </label>
      {group === 'pokemon' && (
        <label className="builder-toggle">
          <input
            type="checkbox"
            checked={family}
            onChange={(event) => setFamily(event.target.checked)}
          />
          {t('filterBuilder.family')}
        </label>
      )}
      {group === 'moves' && (
        <label className="builder-field">
          {t('filterBuilder.moveSlot')}
          <select value={slot} onChange={(event) => setSlot(event.target.value)}>
            <option value="">{t('filterBuilder.anySlot')}</option>
            <option value="1">{t('filterBuilder.fastSlot')}</option>
            <option value="2">{t('filterBuilder.firstCharged')}</option>
            <option value="3">{t('filterBuilder.secondCharged')}</option>
          </select>
        </label>
      )}
      {searchChoices && (
        <label className="builder-field">
          {t('filterBuilder.findValue')}
          <input type="search" value={query} onChange={(event) => setQuery(event.target.value)} />
        </label>
      )}
      {visible.length > 0 && (
        <div className="builder-options">
          {visible.map(({ value, label }, index) => (
            <label key={`${value}:${index}`} className="builder-toggle">
              <input
                type="checkbox"
                checked={selected.includes(include(value))}
                onChange={() => toggle(value)}
              />
              {label}
            </label>
          ))}
        </div>
      )}
      {catalogMatches.length > 12 && <p className="builder-note">{t('filterBuilder.refine')}</p>}
      {searchChoices && visible.length === 0 && (!hasCatalog || catalogStatus === 'success') && (
        <p role="status">{t('filterBuilder.noMatches')}</p>
      )}
      {hasCatalog && catalogStatus === 'pending' && (
        <p role="status">{t('filterBuilder.loadingChoices')}</p>
      )}
      {hasCatalog && catalogStatus === 'error' && (
        <div role="status">
          {t('filterBuilder.choicesError')}{' '}
          <button className="builder-link" type="button" onClick={retryCatalog}>
            {t('filterBuilder.retry')}
          </button>
        </div>
      )}
      {rangeKinds.length > 0 && <RangeFields kinds={rangeKinds} onAdd={add} />}
      {custom && (
        <div className="builder-custom">
          <label className="builder-field">
            {t(group === 'tags' ? 'filterBuilder.tagName' : 'filterBuilder.customValue')}
            <input value={text} onChange={(event) => setText(event.target.value)} />
          </label>
          <button
            className="button button-secondary"
            type="button"
            disabled={!customValue}
            onClick={() => {
              if (customValue) add(customValue)
              setText('')
            }}
          >
            {t('filterBuilder.addValue')}
          </button>
        </div>
      )}
      {selected.length > 0 && (
        <ul className="builder-selected" aria-label={t('filterBuilder.selected')}>
          {selected.map((term) => (
            <li key={term}>
              <button
                type="button"
                className="builder-chip"
                aria-label={t('filterBuilder.removeValue', { value: labelTerm(term) })}
                onClick={() => setSelected((current) => current.filter((item) => item !== term))}
              >
                {labelTerm(term)} <span aria-hidden="true">×</span>
              </button>
            </li>
          ))}
        </ul>
      )}
      <div className="builder-actions">
        <button type="button" className="button button-secondary" onClick={onCancel}>
          {t('filterBuilder.cancel')}
        </button>
        <button
          type="button"
          className="button"
          disabled={selected.length === 0}
          onClick={() => onApply(selected)}
        >
          {t('filterBuilder.apply')}
        </button>
      </div>
    </div>
  )
}

function RangeFields({
  kinds,
  onAdd,
}: {
  kinds: readonly RangeKind[]
  onAdd: (term: string) => void
}) {
  const { t } = useTranslation()
  const [kind, setKind] = useState<RangeKind>(kinds[0] ?? 'dex')
  const [low, setLow] = useState('')
  const [high, setHigh] = useState('')
  const value = rangeTerm(kind, low, high)
  const { min } = ranges[kind]
  return (
    <fieldset className="builder-range">
      <legend>{t('filterBuilder.addRange')}</legend>
      <label className="builder-field">
        {t('filterBuilder.rangeField')}
        <select
          value={kind}
          onChange={(event) => {
            setKind(event.target.value as RangeKind)
            setLow('')
            setHigh('')
          }}
        >
          {kinds.map((key) => (
            <option key={key} value={key}>
              {t(`filterBuilder.fields.${key}`)}
            </option>
          ))}
        </select>
      </label>
      <div className="builder-range-bounds">
        <label className="builder-field">
          {t('filterBuilder.minimum')}
          <input
            inputMode="numeric"
            type="number"
            min={min}
            value={low}
            onChange={(event) => setLow(event.target.value)}
          />
        </label>
        <label className="builder-field">
          {t('filterBuilder.maximum')}
          <input
            inputMode="numeric"
            type="number"
            min={min}
            value={high}
            onChange={(event) => setHigh(event.target.value)}
          />
        </label>
      </div>
      <button
        type="button"
        className="button button-secondary"
        disabled={!value}
        onClick={() => {
          if (value) onAdd(value)
          setLow('')
          setHigh('')
        }}
      >
        {t('filterBuilder.addValue')}
      </button>
    </fieldset>
  )
}
