import { useTranslation } from 'react-i18next'
import type { PokemonType } from './pokemonTypes'

/** A type in its own colour (Water, Ghost, Dragon…), on badges, move chips and charts everywhere. */
export function TypeBadge({
  type,
  size = 'normal',
}: {
  type: PokemonType
  size?: 'normal' | 'large'
}) {
  const { t } = useTranslation()
  return (
    <span className="ui-type" data-type={type} data-size={size}>
      {t(`types.${type}`)}
    </span>
  )
}
