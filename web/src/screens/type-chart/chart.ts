import { isPokemonType, type PokemonType } from '../../ui/pokemonTypes'
import powers from './matchups.json'

export const chartTypes = Object.keys(powers).filter(isPokemonType)
const columns = new Map(chartTypes.map((type, index) => [type, index]))

// Matchups are powers of 1.6. Adding the powers before converting keeps dual-type cancellation exact.
export function matchupPower(attacking: PokemonType, defending: PokemonType): number {
  return powers[attacking][columns.get(defending)!]!
}

export function defendingPower(attacking: PokemonType, defenders: readonly PokemonType[]): number {
  return [...new Set(defenders)].reduce((power, type) => power + matchupPower(attacking, type), 0)
}

export function multiplier(power: number): number {
  return 1.6 ** power
}

export const chartModes = ['defending', 'attacking', 'full'] as const
export type ChartMode = (typeof chartModes)[number]

export function readChart(params: URLSearchParams) {
  const mode = chartModes.find((value) => value === params.get('mode')) ?? 'defending'
  const defenders = [
    ...new Set((params.get('types') ?? '').toLowerCase().split(',').filter(isPokemonType)),
  ].slice(0, 2)
  const attack = (params.get('attack') ?? '').toLowerCase()
  return { mode, defenders, attacker: isPokemonType(attack) ? attack : null }
}

export function writeChart({ mode, defenders, attacker }: ReturnType<typeof readChart>) {
  const params = new URLSearchParams()
  if (mode !== 'defending') params.set('mode', mode)
  if (defenders.length) params.set('types', defenders.join(','))
  if (attacker) params.set('attack', attacker)
  return params
}
