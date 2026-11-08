import { useTranslation } from 'react-i18next'
import { Icon } from '../../components/icons'

/** The Pokédex's own search box (B1): filters as you type; ✕ clears it; ? explains the search. */
export function SearchBox({
  value,
  onChange,
  onHelp,
}: {
  value: string
  onChange: (value: string) => void
  onHelp: () => void
}) {
  const { t } = useTranslation()
  return (
    <div className="pokedex-search" role="search">
      <Icon name="search" size={19} />
      <input
        type="search"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={t('pokedex.searchPlaceholder')}
        aria-label={t('pokedex.searchLabel')}
        autoComplete="off"
        autoCapitalize="off"
        autoCorrect="off"
        spellCheck={false}
        enterKeyHint="search"
      />
      {value && (
        <button
          type="button"
          className="pokedex-search-button"
          aria-label={t('pokedex.clearSearch')}
          onClick={() => onChange('')}
        >
          <Icon name="close" size={16} strokeWidth={2.6} />
        </button>
      )}
      <button
        type="button"
        className="pokedex-search-help"
        aria-label={t('pokedex.help')}
        title={t('pokedex.help')}
        onClick={onHelp}
      >
        ?
      </button>
    </div>
  )
}
