import { useRef, type KeyboardEvent } from 'react'

export interface ChoiceRow<T extends string> {
  value: T
  label: string
  /** The row's own language, when its text is in another one (Settings → Language: "Português (Portugal)"). */
  lang?: string
}

/**
 * A card of rows, one ticked (Settings → Language, F2): a radio group, like the segmented switch but for longer
 * labels. Tab reaches the ticked row, the arrow keys move the tick.
 */
export function ChoiceRows<T extends string>({
  label,
  options,
  value,
  onChange,
}: {
  /** What the choice is about, for screen readers (e.g. "Language"). */
  label: string
  options: readonly ChoiceRow<T>[]
  value: T
  onChange: (value: T) => void
}) {
  const rows = useRef<(HTMLButtonElement | null)[]>([])

  function onKeyDown(event: KeyboardEvent, index: number) {
    const step = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[event.key]
    if (!step) {
      return
    }
    event.preventDefault()
    const next = (index + step + options.length) % options.length
    onChange(options[next]!.value)
    rows.current[next]?.focus()
  }

  return (
    <div className="ui-card ui-choices" role="radiogroup" aria-label={label}>
      {options.map((option, index) => {
        const selected = option.value === value
        return (
          <button
            key={option.value}
            ref={(element) => {
              rows.current[index] = element
            }}
            type="button"
            role="radio"
            lang={option.lang}
            aria-checked={selected}
            tabIndex={selected ? 0 : -1}
            className="ui-choice"
            onClick={() => onChange(option.value)}
            onKeyDown={(event) => onKeyDown(event, index)}
          >
            {option.label}
            {selected && (
              <span className="ui-choice-check" aria-hidden="true">
                ✓
              </span>
            )}
          </button>
        )
      })}
    </div>
  )
}
