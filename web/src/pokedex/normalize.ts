/**
 * How the Pokédex search compares text: lowercase, without accents ("Elétrico" → "eletrico", "Pokédex" → "pokedex"),
 * apostrophes and dots dropped ("Farfetch'd" → "farfetchd", "Mr. Mime" → "mr mime"), anything else that isn't a
 * letter or a digit becomes a space, and spaces are squeezed.
 */
export function normalize(text: string): string {
  return text
    .normalize('NFD')
    .replace(/\p{M}+/gu, '')
    .toLowerCase()
    .replace(/['’.]/g, '')
    .replace(/[^\p{L}\p{N}]+/gu, ' ')
    .trim()
}

/** {@link normalize} without spaces, for move names typed without them ("firepunch"). */
export function compact(text: string): string {
  return normalize(text).replace(/ /g, '')
}
