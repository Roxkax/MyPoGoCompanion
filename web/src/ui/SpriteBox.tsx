import { useState } from 'react'

/**
 * A Pokémon picture in a square box. Sprites are trimmed to their content and scaled to fill the box, so a small
 * Pokémon looks as big as a large one (design conventions). Without a picture (none yet, or it failed to load) the
 * box shows a soft placeholder of the same size, so lines never jump.
 */
export function SpriteBox({
  src,
  alt,
  size = 64,
}: {
  src?: string | null
  /** What the picture shows; empty when the name is written next to it. */
  alt: string
  size?: number
}) {
  const [failed, setFailed] = useState<string | null>(null)
  const showImage = !!src && failed !== src
  return (
    <span className="ui-sprite" style={{ width: size, height: size }}>
      {showImage ? (
        <img
          src={src}
          alt={alt}
          width={size}
          height={size}
          loading="lazy"
          decoding="async"
          onError={() => setFailed(src)}
        />
      ) : (
        <span
          className="ui-sprite-empty"
          role={alt ? 'img' : undefined}
          aria-label={alt || undefined}
        />
      )}
    </span>
  )
}
