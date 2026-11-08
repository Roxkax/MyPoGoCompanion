import { describe, expect, it } from 'vitest'
import { formatsFor } from '../../i18n/format'
import { createI18n } from '../../i18n/i18n'
import { preparePokedex } from '../../pokedex/data'
import { parseQuery, type Term } from '../../pokedex/search'
import { samplePokedex } from '../../testing/pokedex'
import { markerLabel, rangeLabel, termLabel } from './labels'

const t = createI18n('en').t
const formats = formatsFor('en')
const pokedex = preparePokedex(samplePokedex)
const moveName = (id: string) => pokedex.movesById.get(id)?.nameEn ?? id

function read(query: string): string {
  const term = parseQuery(query, pokedex).groups[0]?.alternatives[0]?.term as Term
  return termLabel(term, t, formats, moveName)
}

describe('how the search read a word', () => {
  it.each([
    ['garch', 'Name “garch”'],
    ['445', '#445'],
    ['1-151', '#1–151'],
    ['387-', '#387 and up'],
    ['fire', 'Type: Fire'],
    ['gen4', 'Sinnoh (gen 4)'],
    ['hisui', 'Hisui (#899–905)'],
    ['hisuian', 'Has a Hisuian form'],
    ['mega', 'Mega'],
    ['primal', 'Primal'],
    ['dynamax', 'Can Dynamax'],
    ['legendary', 'Legendary'],
    ['ultrabeast', 'Ultra Beast'],
    ['released', 'Released'],
    ['unreleased', 'Not released'],
    ['@earthquake', 'Move: Earthquake'],
    ['@fire p', 'Move: Fire Punch'],
    ['@f', 'Moves “f” (2)'],
    ['@fire', 'Fire move'],
    ['@1fire', 'Fire fast move'],
    ['@2fire', 'Fire charged move'],
    ['+pikachu', 'Family of “pikachu”'],
    ['atk250-', 'Attack ≥ 250'],
    ['sta-150', 'Stamina ≤ 150'],
    ['def200-300', 'Defense 200–300'],
    ['cp4000', 'Max CP (L50) = 4,000'],
    ['xyzzy', '“xyzzy”: not recognised'],
  ])('%s → %s', (query, label) => {
    expect(read(query)).toBe(label)
  })

  it('names an unknown generation after Kanto rather than failing', () => {
    expect(termLabel({ kind: 'generation', generation: 12 }, t, formats, moveName)).toBe(
      'Kanto (gen 12)',
    )
  })

  it('names moves by id when the Pokédex has no name for them', () => {
    expect(
      termLabel({ kind: 'move', text: 'x', ids: new Set(['NOPE']) }, t, formats, moveName),
    ).toBe('Move: NOPE')
  })
})

describe('markers and ranges', () => {
  it('falls back to the version kind when a version has no label', () => {
    const [main] = pokedex.entries[0]!.versions
    const plain = { ...main!, labelEn: null, labelPt: null }
    expect(markerLabel({ kind: 'mega', version: plain }, 'en', t)).toBe('Mega')
    expect(markerLabel({ kind: 'regional', region: 'galarian', version: plain }, 'pt-PT', t)).toBe(
      'Galarian',
    )
    expect(markerLabel({ kind: 'form', version: plain }, 'en', t)).toBe('Charizard')
    expect(markerLabel({ kind: 'gmax' }, 'en', t)).toBe('G-Max')
  })

  it('writes a range with only a top', () => {
    expect(rangeLabel('maxCp', { min: null, max: 1500 }, t, formats)).toBe('Max CP (L50) ≤ 1,500')
  })
})
