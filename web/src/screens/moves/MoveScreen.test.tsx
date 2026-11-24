import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import type { Schemas } from '../../api/client'
import { LanguageStore } from '../../i18n/language'
import { renderApp } from '../../testing/app'
import { MemoryStorage } from '../../testing/auth'

const move: Schemas['PageMove'] = {
  id: 'EARTH_POWER',
  nameEn: 'Earth Power',
  namePt: 'Poder da Terra',
  type: 'ground',
  kind: 'Charged',
  raidPower: 100,
  raidDurationMs: 3500,
  raidEnergy: -50,
  pvpPower: 90,
  pvpEnergy: -50,
  pvpTurns: null,
  pvpBuffChance: 0.1,
  pvpSelfAttack: 0,
  pvpSelfDefense: 0,
  pvpOpponentAttack: 0,
  pvpOpponentDefense: -1,
}

const learner: Schemas['MoveLearner'] = {
  dex: 445,
  formId: 'garchomp',
  nameEn: 'Garchomp',
  namePt: null,
  types: ['dragon', 'ground'],
  attack: 261,
  sprite: null,
  availability: 'EliteTm',
  megaToo: true,
  primalToo: false,
}

const event: Schemas['MoveEvent'] = {
  eventId: '4e04dbf4-d498-42af-bc61-57ed3b864567',
  formId: 'garchomp',
  nameEn: 'Community Day: Gible',
  namePt: 'Dia Comunitário: Gible',
  pokemonNameEn: 'Garchomp',
  pokemonNamePt: null,
  howEn: 'Evolve Gabite before 22:00',
  howPt: 'Evolui Gabite até às 22:00',
  startsAt: '2026-11-07T14:00:00',
  endsAt: '2026-11-07T17:00:00',
  timeZone: null,
}

let answer: () => Response

function response(status: number, data: unknown) {
  return Response.json(data, { status })
}

beforeEach(() => {
  answer = () => response(200, { move, learners: [learner], events: [event] })
  vi.stubGlobal('fetch', (input: RequestInfo | URL) =>
    new Request(input).url.endsWith('/moves/earth-power')
      ? Promise.resolve(answer())
      : new Promise(() => {}),
  )
})
afterEach(() => vi.unstubAllGlobals())

it('shows both battle modes and keeps dated evolution instructions beyond event end', async () => {
  renderApp('/pokedex/moves/earth-power')

  expect(await screen.findByRole('heading', { name: 'Earth Power' })).toBeVisible()
  const raids = within(screen.getByRole('region', { name: 'Raids & gyms' }))
  expect(raids.getByText('−50')).toBeVisible()
  expect(raids.getByText('2 bars')).toBeVisible()
  expect(screen.getByText('10% chance')).toBeVisible()
  expect(screen.getByText('Evolve Gabite before 22:00', { exact: false })).toBeVisible()
  expect(screen.getByText(/17:00/)).toBeVisible()
  const elite = within(screen.getByRole('region', { name: 'Elite TM only' }))
  expect(elite.getByRole('link')).toHaveAttribute('href', '/pokedex/445?version=garchomp')
  expect(elite.getByText('Mega too')).toBeVisible()
  expect(screen.getByRole('region', { name: 'Legacy' })).toHaveTextContent('None')
})

it('localizes available names and instructions while preserving missing raid values', async () => {
  answer = () =>
    response(200, {
      move: { ...move, kind: 'Fast', raidDurationMs: 0, pvpEnergy: 8, pvpTurns: 3 },
      learners: [
        {
          ...learner,
          availability: 'Normal',
          namePt: 'Garchomp variante',
          megaToo: false,
          primalToo: true,
        },
      ],
      events: [{ ...event, endsAt: null, timeZone: 'Europe/Lisbon' }],
    })
  const languages = new LanguageStore(new MemoryStorage(), ['pt-PT'])
  renderApp('/pokedex/moves/earth-power', { languages, layout: 'desktop' })

  expect(await screen.findByRole('heading', { name: 'Poder da Terra' })).toBeVisible()
  expect(screen.getAllByText('—')).toHaveLength(3)
  expect(screen.getByText('+8')).toBeVisible()
  expect(screen.getByText('Garchomp variante')).toBeVisible()
  expect(screen.getByText('Também Primitivo')).toBeVisible()
  expect(screen.getByText('Evolui Gabite até às 22:00', { exact: false })).toBeVisible()
  expect(screen.getByText('Europe/Lisbon', { exact: false })).toBeVisible()
})

it('expands and collapses only the requested group on phones', async () => {
  const learners = Array.from({ length: 9 }, (_, index) => ({
    ...learner,
    formId: `learner-${index}`,
    nameEn: `Learner ${index + 1}`,
    availability: 'Normal',
  }))
  answer = () => response(200, { move, learners, events: [] })
  renderApp('/pokedex/moves/earth-power')
  const group = within(await screen.findByRole('region', { name: 'Can learn it' }))

  expect(group.getAllByRole('link')).toHaveLength(8)
  await userEvent.click(group.getByRole('button', { name: 'Show all 9' }))
  expect(group.getAllByRole('link')).toHaveLength(9)
  await userEvent.click(group.getByRole('button', { name: 'Show fewer' }))
  expect(group.getAllByRole('link')).toHaveLength(8)
})

it('allows a retry after a server error and shows a distinct missing-move state', async () => {
  answer = () => response(500, { status: 500 })
  renderApp('/pokedex/moves/earth-power')
  const retry = await screen.findByRole('button', { name: 'Try again' })
  answer = () => response(404, { status: 404 })
  await userEvent.click(retry)
  expect(await screen.findByRole('heading', { name: 'Move not found' })).toBeVisible()
})
