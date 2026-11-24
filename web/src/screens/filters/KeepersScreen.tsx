import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { $api } from '../../api/client'
import type { components } from '../../api/generated/schema'
import { LoadError } from '../../components/LoadError'
import { useFormats } from '../../i18n/languageContext'
import { useIsDesktop } from '../../layout/useMediaQuery'
import { SegmentedSwitch } from '../../ui/SegmentedSwitch'

type Part = components['schemas']['KeeperFilterPart']
type Kind = Part['kind']

const cards = [
  { kind: 'GreatLeague', tag: 'GL', icon: 'GL' },
  { kind: 'UltraLeague', tag: 'UL', icon: 'UL' },
  { kind: 'Raid', tag: 'Raid', icon: 'R' },
  { kind: 'MaxBattle', tag: 'Max', icon: 'M' },
  { kind: 'Mega', tag: 'Mega', icon: 'Me' },
] as const satisfies readonly { kind: Kind; tag: string; icon: string }[]

export function KeepersScreen() {
  const { t } = useTranslation()
  const desktop = useIsDesktop()
  const query = $api.useQuery('get', '/keeper-filters')
  if (query.isPending) return <p role="status">{t('keepers.loading')}</p>
  if (!query.data) return <LoadError error={query.error} onRetry={() => void query.refetch()} />
  const parts = query.data.filters
  if (!parts.length) {
    return (
      <section className="ui-card ui-card-padded">
        <h2>{t('keepers.emptyTitle')}</h2>
        <p>{t('keepers.emptyText')}</p>
      </section>
    )
  }

  return (
    <div className="keepers">
      {!desktop && (
        <details className="keepers-help ui-card ui-card-padded">
          <summary>{t('keepers.help')}</summary>
          <HowTo />
        </details>
      )}
      <div className="keepers-cards">
        {cards.map((card) => (
          <KeeperCard
            key={card.kind}
            {...card}
            parts={parts.filter((part) => part.kind === card.kind)}
          />
        ))}
      </div>
      {desktop && (
        <aside className="ui-card ui-card-padded">
          <h2>{t('keepers.help')}</h2>
          <HowTo />
        </aside>
      )}
    </div>
  )
}

function HowTo() {
  const { t } = useTranslation()
  return (
    <>
      <ol>
        <li>{t('keepers.howCopy')}</li>
        <li>{t('keepers.howPaste')}</li>
        <li>{t('keepers.howTag')}</li>
        <li>{t('keepers.howRepeat')}</li>
        <li>{t('keepers.howFind')}</li>
      </ol>
      <p>{t('keepers.english')}</p>
    </>
  )
}

function KeeperCard({
  kind,
  tag,
  icon,
  parts,
}: {
  kind: Kind
  tag: string
  icon: string
  parts: readonly Part[]
}) {
  const { t } = useTranslation()
  const attackOptions = kind === 'Raid' || kind === 'MaxBattle'
  const options =
    kind === 'Mega'
      ? [
          { value: 'with-pre-evolutions', label: t('keepers.yes') },
          { value: '', label: t('keepers.no') },
        ]
      : [
          { value: '', label: t('keepers.attackRange') },
          { value: 'attack-15', label: t('keepers.attackPerfect') },
        ]
  const availableOptions = options.filter((option) =>
    parts.some((part) => part.variant === option.value),
  )
  const [choice, setChoice] = useState(kind === 'Mega' ? 'with-pre-evolutions' : '')
  const variant = availableOptions.some((option) => option.value === choice)
    ? choice
    : (availableOptions[0]?.value ?? '')
  const show = parts.filter((part) => part.variant === variant)
  const title = t(`keepers.cards.${kind}.title`)

  if (!show.length) return null

  const label = t(kind === 'Mega' ? 'keepers.preEvolutions' : 'keepers.attack')
  return (
    <section className="keeper-card ui-card ui-card-padded" data-kind={kind} aria-label={title}>
      <header>
        <span className="keeper-icon" aria-hidden="true">
          {icon}
        </span>
        <h2>{title}</h2>
        <code className="keeper-tag">#{tag}</code>
      </header>
      <p>{t(`keepers.cards.${kind}.description`)}</p>
      {(attackOptions || kind === 'Mega') && availableOptions.length > 1 && (
        <div className="keeper-options">
          <span>{label}</span>
          <SegmentedSwitch
            label={label}
            options={availableOptions}
            value={variant}
            onChange={setChoice}
          />
        </div>
      )}
      <div className="keeper-parts">
        {show.map((part) => (
          <KeeperPart key={`${part.part}:${part.searchString}`} part={part} />
        ))}
      </div>
    </section>
  )
}

function KeeperPart({ part }: { part: Part }) {
  const { t } = useTranslation()
  const formats = useFormats()
  const [status, setStatus] = useState<'idle' | 'copied' | 'manual'>('idle')
  const range =
    part.fromDex != null && part.toDex != null
      ? `#${formats.number(part.fromDex)}–${formats.number(part.toDex)}`
      : null
  const number = t('keepers.part', { number: formats.number(part.part) })
  const action = t(status === 'copied' ? 'keepers.copied' : 'keepers.copy')

  async function copy() {
    try {
      await navigator.clipboard.writeText(part.searchString)
      setStatus('copied')
    } catch {
      setStatus('manual')
    }
  }

  return (
    <div className="keeper-part">
      {range && (
        <span className="keeper-range">
          {number} <span>{range}</span>
        </span>
      )}
      <code className="keeper-preview">{part.searchString}</code>
      <button
        type="button"
        className="button keeper-copy"
        data-copied={status === 'copied' || undefined}
        aria-label={range ? t('keepers.copyPart', { action, part: number, range }) : action}
        onClick={() => void copy()}
      >
        <span aria-live="polite">{action}</span>
        {range && (
          <span className="keeper-copy-range">
            {number} · {range}
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
            value={part.searchString}
            onFocus={(event) => event.currentTarget.select()}
          />
        </label>
      )}
    </div>
  )
}
