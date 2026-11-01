import { typeName, type PokemonType } from './pokemonTypes'

/** A type in its own colour (Water, Ghost, Dragon…), on badges, move chips and charts everywhere. */
export function TypeBadge({
  type,
  size = 'normal',
}: {
  type: PokemonType
  size?: 'normal' | 'large'
}) {
  return (
    <span className="ui-type" data-type={type} data-size={size}>
      {typeName(type)}
    </span>
  )
}
