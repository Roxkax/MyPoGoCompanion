import { useRef, type KeyboardEvent } from 'react'

export interface SegmentOption<T extends string> {
  value: T
  label: string
  /** A number after the label (e.g. "Charged 235"). */
  count?: number
}

/**
 * Two to four options side by side, one selected (Settings → Theme, Fast/Charged, Raids/PvP…). A radio group:
 * Tab reaches the selected option, the arrow keys move the selection.
 */
export function SegmentedSwitch<T extends string>({
  label,
  options,
  value,
  onChange,
}: {
  /** What the choice is about, for screen readers (e.g. "Theme"). */
  label: string
  options: readonly SegmentOption<T>[]
  value: T
  onChange: (value: T) => void
}) {
  const buttons = useRef<(HTMLButtonElement | null)[]>([])

  function onKeyDown(event: KeyboardEvent, index: number) {
    const step = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[event.key]
    if (!step) {
      return
    }
    event.preventDefault()
    const next = (index + step + options.length) % options.length
    onChange(options[next]!.value)
    buttons.current[next]?.focus()
  }

  return (
    <div className="ui-seg" role="radiogroup" aria-label={label}>
      {options.map((option, index) => {
        const selected = option.value === value
        return (
          <button
            key={option.value}
            ref={(element) => {
              buttons.current[index] = element
            }}
            type="button"
            role="radio"
            aria-checked={selected}
            tabIndex={selected ? 0 : -1}
            className="ui-seg-option"
            onClick={() => onChange(option.value)}
            onKeyDown={(event) => onKeyDown(event, index)}
          >
            {option.label}
            {option.count !== undefined && <i className="num">{option.count}</i>}
          </button>
        )
      })}
    </div>
  )
}
