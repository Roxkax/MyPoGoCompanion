import { useState } from 'react'

/**
 * A Pokémon picture in a square box. Sprites are trimmed to their content and scaled to fill the box, so a small
 * Pokémon looks as big as a large one (design conventions). `src` may list several pictures, best first
 * (`pictureSources` in ./sprites.ts): when one fails to load the next is tried. Without a picture (none, or every one
 * failed) the box shows a soft placeholder of the same size, so lines never jump.
 */
export function SpriteBox({
  src,
  alt,
  size = 64,
}: {
  src?: string | readonly string[] | null
  /** What the picture shows; empty when the name is written next to it. */
  alt: string
  size?: number
}) {
  const sources = (typeof src === 'string' ? [src] : (src ?? [])).filter(Boolean)
  const key = sources.join('\n')
  // How many of these sources failed; a new list starts again from the first.
  const [failed, setFailed] = useState<{ key: string; count: number }>({ key, count: 0 })
  const index = failed.key === key ? failed.count : 0
  const current = sources[index]
  return (
    <span className="ui-sprite" style={{ width: size, height: size }}>
      {current ? (
        <img
          key={current}
          src={current}
          alt={alt}
          width={size}
          height={size}
          loading="lazy"
          decoding="async"
          onError={() => setFailed({ key, count: index + 1 })}
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
