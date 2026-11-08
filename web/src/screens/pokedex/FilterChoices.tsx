import { useTranslation } from 'react-i18next'
import { regionsByGeneration } from '../../pokedex/data'
import { toggleType } from '../../pokedex/filters'
import { Chip } from '../../ui/Chip'
import { pokemonTypes, type PokemonType } from '../../ui/pokemonTypes'

/** The 18 types to pick from, up to two (a third replaces the first): the filter sheet and the desktop's Type ▾. */
export function TypeChoices({
  types,
  onChange,
}: {
  types: readonly PokemonType[]
  onChange: (types: PokemonType[]) => void
}) {
  const { t } = useTranslation()
  return (
    <>
      <p className="filter-hint">{t('pokedex.sheet.typeHint')}</p>
      <div className="type-grid">
        {pokemonTypes.map((type) => (
          <button
            key={type}
            type="button"
            className="type-choice"
            data-type={type}
            aria-pressed={types.includes(type)}
            onClick={() => onChange(toggleType(types, type))}
          >
            {t(`types.${type}`)}
          </button>
        ))}
      </div>
    </>
  )
}

/** All, then the nine regions (any of them): the filter sheet and the desktop's Generation ▾. */
export function GenerationChoices({
  generations,
  onChange,
}: {
  generations: readonly number[]
  onChange: (generations: number[]) => void
}) {
  const { t } = useTranslation()
  return (
    <div className="wrap-row">
      <Chip selected={!generations.length} onClick={() => onChange([])}>
        {t('pokedex.sheet.all')}
      </Chip>
      {regionsByGeneration.map((region, index) => {
        const generation = index + 1
        const chosen = generations.includes(generation)
        return (
          <Chip
            key={region}
            selected={chosen}
            onClick={() =>
              onChange(
                chosen
                  ? generations.filter((other) => other !== generation)
                  : [...generations, generation].sort((a, b) => a - b),
              )
            }
          >
            {t(`pokedex.regions.${region}`)}
          </Chip>
        )
      })}
    </div>
  )
}
