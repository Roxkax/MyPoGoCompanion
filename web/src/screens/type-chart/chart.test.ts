import { expect, it } from 'vitest'
import {
  chartTypes,
  defendingPower,
  matchupPower,
  multiplier,
  readChart,
  writeChart,
} from './chart'

it('uses all 18 types and follows Pokémon GO for reduced damage', () => {
  expect(new Set(chartTypes).size).toBe(18)
  expect(multiplier(matchupPower('ground', 'flying'))).toBeCloseTo(0.390625, 10)
  expect(multiplier(matchupPower('ground', 'grass'))).toBeCloseTo(0.625, 10)
  expect(multiplier(matchupPower('ground', 'electric'))).toBe(1.6)
})

it('combines weaknesses and resistances, including exact cancellation', () => {
  expect(multiplier(defendingPower('grass', ['water', 'ground']))).toBeCloseTo(2.56, 10)
  expect(multiplier(defendingPower('electric', ['water', 'ground']))).toBeCloseTo(0.625, 10)
  expect(multiplier(defendingPower('ice', ['water', 'ground']))).toBe(1)
  expect(multiplier(defendingPower('ice', ['dragon', 'dragon']))).toBe(1.6)
  expect(multiplier(defendingPower('ground', ['flying', 'bug']))).toBeCloseTo(0.244140625, 10)
})

it('keeps valid deep-link selections and ignores duplicates and unknown values', () => {
  const params = new URLSearchParams(
    'mode=attacking&types=WATER,water,sound,ground,fire&attack=GROUND',
  )
  const selection = readChart(params)
  expect(selection).toEqual({
    mode: 'attacking',
    defenders: ['water', 'ground'],
    attacker: 'ground',
  })
  expect(readChart(writeChart(selection))).toEqual(selection)
  expect(readChart(new URLSearchParams('mode=invalid&attack=invalid'))).toEqual({
    mode: 'defending',
    defenders: [],
    attacker: null,
  })
})
