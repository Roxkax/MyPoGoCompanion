import { describe, expect, it } from 'vitest'
import {
  defaultOrder,
  filterMoves,
  moveName,
  noFilters,
  readFilters,
  setKind,
  setMode,
  sortBy,
  sortKeys,
  writeFilters,
  type Move,
  type MoveFilters,
} from './view'

function move(id: string, changes: Partial<Move> = {}): Move {
  return {
    id,
    nameEn: id,
    namePt: null,
    type: 'dragon',
    kind: 'Charged',
    raidPower: 100,
    raidDurationMs: 2000,
    raidEnergy: -50,
    pvpPower: 90,
    pvpEnergy: -45,
    pvpTurns: null,
    pvpBuffChance: null,
    pvpSelfAttack: 0,
    pvpSelfDefense: 0,
    pvpOpponentAttack: 0,
    pvpOpponentDefense: 0,
    ...changes,
  }
}

const moves = [
  move('DRAGON_CLAW', {
    nameEn: 'Dragon Claw',
    namePt: 'Garra de Dragão',
    raidPower: 50,
    raidDurationMs: 1500,
    raidEnergy: -33,
    pvpPower: 80,
    pvpEnergy: -50,
  }),
  move('DRACO_METEOR', {
    nameEn: 'Draco Meteor',
    namePt: 'Meteoro do Dragão',
    raidPower: 150,
    raidDurationMs: 3500,
    raidEnergy: -100,
    pvpPower: 150,
    pvpEnergy: -65,
  }),
  move('FIRE_PUNCH', {
    nameEn: 'Fire Punch',
    namePt: 'Soco de Fogo',
    type: 'fire',
    raidPower: 55,
    raidDurationMs: 2000,
    raidEnergy: -33,
    pvpPower: 55,
    pvpEnergy: -40,
  }),
  move('DRAGON_TAIL', {
    nameEn: 'Dragon Tail',
    kind: 'Fast',
    raidPower: 15,
    raidDurationMs: 1000,
    raidEnergy: 9,
    pvpPower: 13,
    pvpEnergy: 9,
    pvpTurns: 3,
  }),
  move('DRAGON_BREATH', {
    nameEn: 'Dragon Breath',
    kind: 'Fast',
    raidPower: 6,
    raidDurationMs: 500,
    raidEnergy: 4,
    pvpPower: 4,
    pvpEnergy: 3,
    pvpTurns: 1,
  }),
]

const ids = (filters: Partial<MoveFilters>, input: Move[] = moves) =>
  filterMoves(input, { ...noFilters, ...filters }, 'en').map((m) => m.id)

describe('moves filters in the address', () => {
  it('defaults to charged raid power and ignores unknown choices', () => {
    expect(readFilters(new URLSearchParams())).toEqual(noFilters)
    expect(
      readFilters(new URLSearchParams('kind=other&type=bad&mode=nope&sort=oops&order=sideways')),
    ).toEqual(noFilters)
    expect(writeFilters(noFilters).toString()).toBe('')
  })

  it('round-trips search, type, kind, mode, sort and reversed order', () => {
    const filters: MoveFilters = {
      q: 'soco de fogo',
      kind: 'fast',
      type: 'fire',
      mode: 'pvp',
      sort: 'pvpTurns',
      order: 'desc',
    }
    expect(writeFilters(filters).toString()).toBe(
      'q=soco+de+fogo&kind=fast&type=fire&mode=pvp&sort=pvpTurns&order=desc',
    )
    expect(readFilters(writeFilters(filters))).toEqual(filters)
    expect(readFilters(new URLSearchParams('type=FIRE&sort=name')).type).toBe('fire')
    expect(readFilters(new URLSearchParams('sort=name')).order).toBe('asc')
    expect(readFilters(new URLSearchParams('sort=raidEnergy')).order).toBe('desc')
  })

  it('keeps an explicitly ascending number sort', () => {
    const filters = { ...noFilters, order: 'asc' as const }
    expect(writeFilters(filters).toString()).toBe('order=asc')
    expect(readFilters(writeFilters(filters))).toEqual(filters)
  })

  it('chooses useful initial ordering and reverses a selected column', () => {
    expect(defaultOrder('raidDurationMs')).toBe('asc')
    expect(defaultOrder('pvpTurns')).toBe('asc')
    expect(defaultOrder('type')).toBe('asc')
    expect(sortBy(noFilters, 'raidPower').order).toBe('asc')
    expect(sortBy(sortBy(noFilters, 'name'), 'name').order).toBe('desc')
    expect(sortBy(noFilters, 'pvpPower').order).toBe('desc')
    for (const sort of sortKeys) {
      const filters = { ...noFilters, sort, order: defaultOrder(sort) }
      expect(readFilters(writeFilters(filters))).toEqual(filters)
    }
  })
})

describe('finding and ordering moves', () => {
  it('filters by kind and type, then orders distinct powers', () => {
    expect(ids({})).toEqual(['DRACO_METEOR', 'FIRE_PUNCH', 'DRAGON_CLAW'])
    expect(ids({ type: 'dragon' })).toEqual(['DRACO_METEOR', 'DRAGON_CLAW'])
    expect(ids({ kind: 'fast' })).toEqual(['DRAGON_TAIL', 'DRAGON_BREATH'])
    expect(ids({ kind: 'fast', type: 'fire' })).toEqual([])
    expect(ids({ order: 'asc' })).toEqual(['DRAGON_CLAW', 'FIRE_PUNCH', 'DRACO_METEOR'])
  })

  it('searches English, Portuguese and IDs without case, accent or spacing differences', () => {
    expect(ids({ q: ' DRAGONclaw ' })).toEqual(['DRAGON_CLAW'])
    expect(ids({ q: 'GARRA de dragao' })).toEqual(['DRAGON_CLAW'])
    expect(ids({ q: 'DRACO_METEOR' })).toEqual(['DRACO_METEOR'])
    expect(ids({ q: '  ' })).toHaveLength(3)
    expect(ids({ q: 'does not exist' })).toEqual([])
  })

  it('uses Portuguese names when available, then English', () => {
    expect(moveName(moves[0]!, 'en')).toBe('Dragon Claw')
    expect(moveName(moves[0]!, 'pt-PT')).toBe('Garra de Dragão')
    expect(moveName(moves[3]!, 'pt-PT')).toBe('Dragon Tail')
    const sample = [move('a', { nameEn: 'Zulu', namePt: 'Água' }), move('b', { nameEn: 'Apple' })]
    expect(filterMoves(sample, { ...noFilters, sort: 'name', order: 'asc' }, 'pt-PT')[0]?.id).toBe(
      'a',
    )
    expect(ids({ sort: 'name', order: 'asc' }, sample)).toEqual(['b', 'a'])
  })

  it('sorts signed energy and timings by the displayed number', () => {
    expect(ids({ sort: 'raidEnergy', order: 'asc' })).toEqual([
      'DRACO_METEOR',
      'DRAGON_CLAW',
      'FIRE_PUNCH',
    ])
    expect(ids({ sort: 'pvpEnergy', order: 'desc' })).toEqual([
      'FIRE_PUNCH',
      'DRAGON_CLAW',
      'DRACO_METEOR',
    ])
    expect(ids({ kind: 'fast', sort: 'pvpTurns', order: 'asc' })).toEqual([
      'DRAGON_BREATH',
      'DRAGON_TAIL',
    ])
    expect(ids({ sort: 'raidDurationMs', order: 'asc' })).toEqual([
      'DRAGON_CLAW',
      'FIRE_PUNCH',
      'DRACO_METEOR',
    ])
  })

  it('sorts type labels in the displayed language, with deterministic name and ID ties', () => {
    const sameName = [move('z', { nameEn: 'Same' }), move('a', { nameEn: 'Same' })]
    expect(ids({}, sameName)).toEqual(['a', 'z'])
    const samePower = [move('a', { nameEn: 'Zulu' }), move('z', { nameEn: 'Apple' })]
    expect(ids({}, samePower)).toEqual(['z', 'a'])
    const sorted = filterMoves(moves, { ...noFilters, sort: 'type', order: 'asc' }, 'en', (type) =>
      type === 'fire' ? 'A' : 'Z',
    )
    expect(sorted.map((m) => m.id)).toEqual(['FIRE_PUNCH', 'DRACO_METEOR', 'DRAGON_CLAW'])
    expect(ids({ sort: 'type', order: 'asc' })).toEqual([
      'DRACO_METEOR',
      'DRAGON_CLAW',
      'FIRE_PUNCH',
    ])
    expect(ids({ sort: 'type' }, [move('unknown', { type: 'unknown' })])).toEqual(['unknown'])
  })

  it('puts missing raid stats and null turns last in both directions', () => {
    const sample = [
      move('missing', { raidDurationMs: 0 }),
      move('known', { pvpTurns: 2 }),
      move('also-missing', { raidDurationMs: 0 }),
    ]
    for (const sort of ['raidPower', 'raidDurationMs', 'raidEnergy', 'pvpTurns'] as const) {
      for (const order of ['asc', 'desc'] as const) {
        expect(ids({ sort, order }, sample)).toEqual(['known', 'also-missing', 'missing'])
      }
    }
  })

  it('does not mutate the cached response', () => {
    const input = [...moves]
    filterMoves(input, noFilters, 'en')
    expect(input).toEqual(moves)
  })
})

describe('switching which numbers are shown', () => {
  it('maps corresponding power and energy columns while keeping direction', () => {
    expect(setMode(noFilters, 'pvp')).toEqual({ ...noFilters, mode: 'pvp', sort: 'pvpPower' })
    expect(setMode(setMode(noFilters, 'pvp'), 'raids')).toEqual(noFilters)
    const energy = { ...noFilters, sort: 'raidEnergy' as const, order: 'asc' as const }
    expect(setMode(energy, 'pvp').sort).toBe('pvpEnergy')
    expect(setMode(energy, 'pvp').order).toBe('asc')
    expect(setMode(setMode(energy, 'pvp'), 'raids')).toEqual(energy)
    expect(setMode({ ...noFilters, sort: 'name' }, 'pvp').sort).toBe('name')
    expect(setMode(noFilters, 'raids')).toEqual(noFilters)
  })

  it('maps fast timing to turns, and charged timing to PvP power', () => {
    const duration = { ...noFilters, sort: 'raidDurationMs' as const, order: 'asc' as const }
    const turns = setMode({ ...duration, kind: 'fast' }, 'pvp')
    expect(turns.sort).toBe('pvpTurns')
    expect(turns.order).toBe('asc')
    expect(setMode(turns, 'raids').sort).toBe('raidDurationMs')
    expect(setMode(duration, 'pvp')).toMatchObject({ sort: 'pvpPower', order: 'desc' })
    expect(setKind(turns, 'charged')).toMatchObject({
      kind: 'charged',
      sort: 'pvpPower',
      order: 'desc',
    })
    expect(setKind(noFilters, 'fast')).toEqual({ ...noFilters, kind: 'fast' })
  })
})
