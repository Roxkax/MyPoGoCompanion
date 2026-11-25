import { describe, expect, it } from 'vitest'
import { customTerm, formatConditions, rangeTerm, readConditions } from './filterBuilder'

const names = new Set(['gible'])

describe('filter builder conditions', () => {
  it('keeps alternatives grouped and preserves unrecognized terms from hand edits', () => {
    const conditions = readConditions('Fire; water | Gible & !4* & my nickname,#Trade team', names)
    expect(conditions).toEqual([
      { group: 'types', terms: ['Fire', 'water'] },
      { group: 'pokemon', terms: ['Gible'] },
      { group: 'appraisal', terms: ['!4*'] },
      { group: 'custom', terms: ['my nickname', '#Trade team'] },
    ])
    expect(formatConditions(conditions ?? [])).toBe('Fire,water&Gible&!4*&my nickname,#Trade team')
  })

  it('keeps mixed recognized alternatives in the same condition', () => {
    expect(readConditions('fire,#raid&!shiny', names)).toEqual([
      { group: 'custom', terms: ['fire', '#raid'] },
      { group: 'special', terms: ['!shiny'] },
    ])
  })

  it.each(['!lucky|', 'fire, ,water', '&fire', 'fire& !'])(
    'cannot open incomplete text %s',
    (text) => {
      expect(readConditions(text, names)).toBeNull()
    },
  )

  it.each([
    ['attack', '3', '4', '3-4attack'],
    ['hpBar', '0', '0', '0hp'],
    ['cp', '', '1500', 'cp-1500'],
    ['distance', '100', '', 'distance 100-'],
    ['dex', '0025', '0025', '25'],
    ['defense', '0', '5', null],
    ['age', '4', '3', null],
    ['year', '2015', '', null],
    ['dex', '', '', null],
  ] as const)('encodes %s between %s and %s as %s', (kind, low, high, expected) => {
    expect(rangeTerm(kind, low, high)).toBe(expected)
  })

  it('prevents a typed tag from creating a second clause', () => {
    expect(customTerm('Raid team', '#')).toBe('#Raid team')
    expect(customTerm('#Raid team', '#')).toBe('#Raid team')
    expect(customTerm('Raid,GL', '#')).toBeNull()
  })
})
