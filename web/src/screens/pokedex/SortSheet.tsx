import { useTranslation } from 'react-i18next'
import { sortBy, sortKeys, type Filters } from '../../pokedex/filters'
import { Sheet } from '../../ui/Sheet'

/** The phone's sort (B1 "Sort: Number ↑"): a column, and tapping the chosen one again reverses it. */
export function SortSheet({
  filters,
  onChange,
  onClose,
}: {
  filters: Filters
  onChange: (filters: Filters) => void
  onClose: () => void
}) {
  const { t } = useTranslation()
  return (
    <Sheet id="pokedex-sort" title={t('pokedex.sortTitle')} onClose={onClose}>
      <ul className="sort-options">
        {sortKeys.map((key) => {
          const chosen = filters.sort === key
          return (
            <li key={key}>
              <button
                type="button"
                className="sort-option"
                aria-pressed={chosen}
                onClick={() => {
                  onChange(sortBy(filters, key))
                  if (!chosen) onClose()
                }}
              >
                <span>{t(`pokedex.columns.${key}`)}</span>
                {chosen && (
                  <span className="sort-option-order">
                    {filters.order === 'asc' ? '↑' : '↓'}{' '}
                    {t(
                      filters.order === 'asc'
                        ? 'pokedex.sortedAscending'
                        : 'pokedex.sortedDescending',
                    )}
                  </span>
                )}
              </button>
            </li>
          )
        })}
      </ul>
    </Sheet>
  )
}
