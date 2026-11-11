import { describe, expect, it } from 'vitest'
import { garchompPage } from '../testing/pokemonPage'
import {
  checkSpread,
  clampIv,
  cpOf,
  dexForSlug,
  evolutionChain,
  evolutionsAfter,
  ivsOfBars,
  keeperRanges,
  leagueCaps,
  leaguePath,
  leagueSpreads,
  matchupsOf,
  movepoolOf,
  multiplierText,
  pickVersion,
  pvpMovesets,
  splitAppearances,
  usageShares,
  versionsOf,
  wallClock,
  type PageAppearance,
  type PokemonPage,
} from './page'

const page = garchompPage
const garchomp = page.forms[0]!
const moves = new Map(page.moves.map((move) => [move.id, move]))

describe('versions', () => {
  it('lists each form and its Shadow, and picks the asked one', () => {
    const versions = versionsOf(page)
    expect(versions.map((v) => v.key)).toEqual(['garchomp', 'garchomp_shadow', 'garchomp_mega'])
    expect(pickVersion(versions, 'garchomp_shadow')?.shadow).toBe(true)
    expect(pickVersion(versions, 'nonsense')?.key).toBe('garchomp')
    expect(pickVersion(versions, null)?.key).toBe('garchomp')
  })

  it('leaves out forms that only look different', () => {
    const withLook: PokemonPage = {
      ...page,
      forms: [
        ...page.forms,
        { ...garchomp, id: 'garchomp_costume', isDefault: false, looksOnly: true, shadow: false },
      ],
    }
    expect(versionsOf(withLook).map((v) => v.key)).not.toContain('garchomp_costume')
  })
})

describe('addresses', () => {
  const species = [
    { dex: 122, nameEn: 'Mr. Mime' },
    { dex: 445, nameEn: 'Garchomp' },
    { dex: 669, nameEn: 'Flabébé' },
  ]
  it('finds the number from the English name, the number or a name with accents', () => {
    expect(dexForSlug(species, 'garchomp')).toBe(445)
    expect(dexForSlug(species, 'Garchomp')).toBe(445)
    expect(dexForSlug(species, 'mr-mime')).toBe(122)
    expect(dexForSlug(species, 'flabebe')).toBe(669)
    expect(dexForSlug(species, '445')).toBe(445)
    expect(dexForSlug(species, '9999')).toBeNull()
    expect(dexForSlug(species, 'missingno')).toBeNull()
  })

  it('links leagues to their tier lists', () => {
    expect(leaguePath('Great')).toBe('/battle/tiers/great-league')
    expect(leaguePath('Master')).toBe('/battle/tiers/master-league')
  })
})

describe('keeper bars', () => {
  it('turns appraisal bars into IVs', () => {
    expect(ivsOfBars(0, 1)).toEqual([0, 5])
    expect(ivsOfBars(3, 4)).toEqual([11, 15])
    expect(ivsOfBars(2, 4)).toEqual([6, 15])
    expect(keeperRanges(page.keeperBoxes[0]!)).toEqual([
      [0, 5],
      [11, 15],
      [6, 15],
    ])
  })
})

describe('IV rank checker', () => {
  const great = leagueSpreads(garchomp, leagueCaps.Great, page.cpMultipliers)
  const master = leagueSpreads(garchomp, leagueCaps.Master, page.cpMultipliers)

  it('matches the stored best IVs (the engine’s numbers)', () => {
    const best = checkSpread(great, { attack: 0, defense: 15, stamina: 14 })
    expect(best).toMatchObject({ rank: 1, share: 1, level: 14, cp: 1495 })
    expect(checkSpread(master, { attack: 15, defense: 15, stamina: 15 })).toMatchObject({
      rank: 1,
      level: 50,
      cp: 4479,
    })
  })

  it('ranks any spread below the best, sharing ranks on equal stat products', () => {
    const check = checkSpread(great, { attack: 15, defense: 15, stamina: 15 })
    expect(check.rank).toBeGreaterThan(1)
    expect(check.share).toBeLessThan(1)
    expect(check.cp).toBeLessThanOrEqual(1500)
    expect(() => checkSpread(great, { attack: 16, defense: 0, stamina: 0 })).toThrow(RangeError)
  })

  it('computes CP like the game', () => {
    expect(cpOf(276, 208, 254, page.cpMultipliers[98]!)).toBe(4479)
    expect(cpOf(1, 1, 1, 0.1)).toBe(10)
  })

  it('keeps typed IVs between 0 and 15', () => {
    expect(clampIv('7')).toBe(7)
    expect(clampIv('22')).toBe(15)
    expect(clampIv('-3')).toBe(0)
    expect(clampIv('')).toBe(0)
  })
})

describe('evolutions', () => {
  it('finds what a form evolves into', () => {
    expect(evolutionsAfter(page, 'gible')).toEqual(['gabite', 'garchomp'])
    expect(evolutionsAfter(page, 'garchomp')).toEqual([])
  })

  it('lays the chain out in stages with the step to each', () => {
    const stages = evolutionChain(page, 'garchomp')
    expect(stages.map((stage) => stage.members.map((m) => m.member.formId))).toEqual([
      ['gible'],
      ['gabite'],
      ['garchomp'],
    ])
    expect(stages[2]?.members[0]?.step?.candy).toBe(100)
  })

  it('shows a lone Pokémon as its only stage', () => {
    const lone: PokemonPage = { ...page, evolutions: [] }
    expect(evolutionChain(lone, 'garchomp').map((s) => s.members.length)).toEqual([1])
    expect(evolutionChain({ ...lone, family: [] }, 'garchomp')).toEqual([])
  })
})

describe('appearances', () => {
  const now = new Date(2026, 9, 27, 18, 30)
  const base: PageAppearance = page.appearances[0]!
  const at = (startsAt: string | null, endsAt: string | null): PageAppearance => ({
    ...base,
    startsAt,
    endsAt,
  })

  it('reads wall-clock times', () => {
    expect(wallClock('2026-11-07T14:00:00')?.getHours()).toBe(14)
    expect(wallClock(null)).toBeNull()
    expect(wallClock('nonsense')).toBeNull()
  })

  it('splits right now, coming and history', () => {
    const split = splitAppearances(
      [
        at('2026-10-20T10:00:00', '2026-10-31T20:00:00'),
        at(null, null),
        at('2026-11-07T14:00:00', '2026-11-07T17:00:00'),
        at('2025-01-01T10:00:00', '2025-01-08T10:00:00'),
        at('2024-01-01T10:00:00', '2024-01-08T10:00:00'),
      ],
      now,
    )
    expect(split.now.map((a) => a.endsAt)).toEqual(['2026-10-31T20:00:00', null])
    expect(split.coming).toHaveLength(1)
    expect(split.history.map((a) => a.endsAt)).toEqual([
      '2025-01-08T10:00:00',
      '2024-01-08T10:00:00',
    ])
  })
})

describe('moves', () => {
  it('splits PvP usage into fast and charged shares', () => {
    const shares = usageShares(garchomp.pvp[0]!.usage, moves)
    expect(shares.fast.map((row) => row.move.id)).toEqual(['DRAGON_TAIL', 'MUD_SHOT'])
    expect(shares.fast.reduce((sum, row) => sum + row.share, 0)).toBeCloseTo(1)
    expect(shares.charged[0]?.move.id).toBe('EARTHQUAKE')
    expect(usageShares([], moves)).toEqual({ fast: [], charged: [] })
  })

  it('lists the movepool by kind and name', () => {
    const pool = movepoolOf(garchomp, moves)
    expect(pool.fast.map((row) => row.move.id)).toEqual(['DRAGON_TAIL', 'MUD_SHOT'])
    expect(pool.charged.find((row) => row.move.id === 'EARTH_POWER')?.availability).toBe('EliteTm')
  })

  it('joins leagues with the same recommended moveset', () => {
    const groups = pvpMovesets(garchomp, false)
    expect(groups.map((group) => group.leagues)).toEqual([['Great', 'Ultra']])
    expect(pvpMovesets(garchomp, true).length).toBeGreaterThan(0)
  })
})

describe('type matchups', () => {
  it('lists weaknesses and resistances, strongest first', () => {
    const { weak, resists } = matchupsOf(garchomp)
    expect(weak.map((m) => m.type)).toEqual(['ice', 'dragon', 'fairy'])
    expect(resists[0]?.type).toBe('electric')
    expect(multiplierText(2.56)).toBe('×2.56')
    expect(multiplierText(0.390625)).toBe('×0.39')
  })
})
