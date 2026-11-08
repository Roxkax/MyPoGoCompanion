import { describe, expect, it } from 'vitest'
import { pictureSources, spriteFolder, spriteUrl } from './sprites'

describe('Pokémon picture addresses', () => {
  it('serves a picture from the web app’s own folder as WebP', () => {
    expect(spriteFolder).toBe('/sprites/')
    expect(spriteUrl('pm37.fALOLA')).toBe('/sprites/pm37.fALOLA.webp')
    expect(spriteUrl('pm6.fMEGA_X.s')).toBe('/sprites/pm6.fMEGA_X.s.webp')
  })

  it('escapes anything that isn’t part of a picture name', () => {
    expect(spriteUrl('a b/c')).toBe('/sprites/a%20b%2Fc.webp')
  })
})

describe('which pictures to try', () => {
  const charizard = { sprite: 'pm6', spriteShiny: 'pm6.s', spriteGigantamax: 'pm6.fGIGANTAMAX' }

  it('shows the normal picture by default', () => {
    expect(pictureSources(charizard)).toEqual(['/sprites/pm6.webp'])
  })

  it('tries the shiny picture first when asked for, then the normal one', () => {
    expect(pictureSources(charizard, { shiny: true })).toEqual([
      '/sprites/pm6.s.webp',
      '/sprites/pm6.webp',
    ])
  })

  it('tries the Gigantamax picture first when asked for (there are no shiny G-Max pictures)', () => {
    expect(pictureSources(charizard, { gigantamax: true, shiny: true })).toEqual([
      '/sprites/pm6.fGIGANTAMAX.webp',
      '/sprites/pm6.webp',
    ])
  })

  it('shows the normal picture when there is no shiny or Gigantamax one', () => {
    const urshifu = { sprite: 'pm892.fRAPID_STRIKE', spriteShiny: null }
    expect(pictureSources(urshifu, { shiny: true })).toEqual(['/sprites/pm892.fRAPID_STRIKE.webp'])
    expect(pictureSources(urshifu, { gigantamax: true })).toEqual([
      '/sprites/pm892.fRAPID_STRIKE.webp',
    ])
  })

  it('has nothing to try when the form has no picture (the box shows its placeholder)', () => {
    expect(pictureSources({})).toEqual([])
    expect(pictureSources({ sprite: null }, { shiny: true })).toEqual([])
  })
})
