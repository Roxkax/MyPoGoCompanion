import { useTranslation } from 'react-i18next'
import { Sheet } from '../../ui/Sheet'

type HelpKey =
  | 'names'
  | 'numbers'
  | 'type'
  | 'region'
  | 'regional'
  | 'version'
  | 'category'
  | 'released'
  | 'move'
  | 'moveType'
  | 'family'
  | 'stats'
  | 'or'
  | 'and'
  | 'not'
  | 'together'

/** What each line explains and its examples (typed as is in both languages; the game's own search words). */
const what: readonly [HelpKey, readonly string[]][] = [
  ['names', ['garchomp', 'chomp']],
  ['numbers', ['445', '1-151']],
  ['type', ['fire', 'fogo']],
  ['region', ['kanto', 'gen4', 'hisui']],
  ['regional', ['hisuian']],
  ['version', ['mega', 'shadow', 'gmax']],
  ['category', ['legendary', 'mythical', 'ultrabeast']],
  ['released', ['unreleased']],
  ['move', ['@earthquake', '@firepunch']],
  ['moveType', ['@fire', '@1fire', '@2fire']],
  ['family', ['+eevee']],
  ['stats', ['atk250-', 'def200-300', 'cp-1500']],
]

const combining: readonly [HelpKey, readonly string[]][] = [
  ['or', ['fire,water,rock']],
  ['and', ['fire&flying', 'fire kanto']],
  ['not', ['dragon !legendary']],
  ['together', ['dragon legendary @outrage']],
]

/** "How search works" (decision 2026-11-08): what the Pokédex search understands, with examples to tap. */
export function SearchHelp({
  onTry,
  onClose,
}: {
  onTry: (query: string) => void
  onClose: () => void
}) {
  const { t } = useTranslation()
  const rows = (list: readonly [HelpKey, readonly string[]][]) => (
    <dl className="search-help">
      {list.map(([key, examples]) => (
        <div key={key} className="search-help-row">
          <dt>
            {examples.map((example) => (
              <button
                key={example}
                type="button"
                className="search-example"
                onClick={() => onTry(example)}
              >
                {example}
              </button>
            ))}
          </dt>
          <dd>{t(`pokedex.searchHelp.${key}`)}</dd>
        </div>
      ))}
    </dl>
  )
  return (
    <Sheet id="search-help" title={t('pokedex.searchHelp.title')} onClose={onClose} wide>
      <p className="search-help-intro">{t('pokedex.searchHelp.intro')}</p>
      <h3 className="group-label">{t('pokedex.searchHelp.what')}</h3>
      {rows(what)}
      <h3 className="group-label">{t('pokedex.searchHelp.combine')}</h3>
      {rows(combining)}
      <p className="search-help-intro">{t('pokedex.searchHelp.versions')}</p>
    </Sheet>
  )
}
