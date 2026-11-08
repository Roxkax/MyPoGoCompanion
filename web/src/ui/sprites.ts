// Where the Pokémon pictures are and which to show (docs/SPRITES.md, decision 2026-11-07). For the MVP the deploys
// build them into the web app's own /sprites/ folder; after the MVP they move to Blob storage and only `spriteFolder`
// changes. The API gives each form its picture names (`pm37.fALOLA`); the daily import already chose the main form's
// picture for a form without its own.

/** The folder the pictures are served from. */
export const spriteFolder = '/sprites/'

/** `/sprites/pm37.fALOLA.webp` for the picture `pm37.fALOLA`. */
export function spriteUrl(name: string): string {
  return `${spriteFolder}${encodeURIComponent(name)}.webp`
}

/** A form's pictures as the API gives them; null or missing when there is none. */
export interface FormPictures {
  sprite?: string | null
  spriteShiny?: string | null
  spriteGigantamax?: string | null
}

/**
 * The pictures to try, best first, for `SpriteBox`'s `src`: the shiny or Gigantamax picture when asked for, then
 * the normal one (no shiny picture → the normal one); none at all → the box's placeholder.
 */
export function pictureSources(
  form: FormPictures,
  { shiny = false, gigantamax = false }: { shiny?: boolean; gigantamax?: boolean } = {},
): string[] {
  const names = [
    gigantamax ? form.spriteGigantamax : null,
    shiny && !gigantamax ? form.spriteShiny : null,
    form.sprite,
  ]
  return names.filter((name): name is string => !!name).map(spriteUrl)
}
