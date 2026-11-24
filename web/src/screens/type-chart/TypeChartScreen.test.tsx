import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { LanguageStore } from '../../i18n/language'
import { currentAddress, renderApp } from '../../testing/app'
import { MemoryStorage } from '../../testing/auth'

beforeEach(() => vi.stubGlobal('fetch', () => new Promise(() => {})))
afterEach(() => vi.unstubAllGlobals())

it('opens a defensive deep link and passes both types to the Pokédex', async () => {
  renderApp('/pokedex/types?types=water,ground')
  const user = userEvent.setup()

  expect(screen.getByText('Grass ×2.56')).toBeInTheDocument()
  expect(screen.getByText('Electric ×0.63')).toBeInTheDocument()
  const link = screen.getByRole('link', { name: 'See Pokémon with this typing' })
  expect(link).toHaveAttribute('href', '/pokedex?type=water%2Cground')

  await user.click(screen.getByRole('button', { name: 'Fire' }))
  expect(currentAddress()).toBe('/pokedex/types?types=ground%2Cfire')
  await user.click(screen.getByRole('button', { name: 'Fire' }))
  expect(currentAddress()).toBe('/pokedex/types?types=ground')
  await user.click(screen.getByRole('button', { name: 'Clear' }))
  expect(screen.queryByRole('link', { name: 'See Pokémon with this typing' })).toBeNull()
})

it('keeps separate attacking and defending choices as the mode changes', async () => {
  renderApp('/pokedex/types?types=water,ground')
  const user = userEvent.setup()

  await user.click(screen.getByRole('radio', { name: 'Attacking' }))
  await user.click(screen.getByRole('button', { name: 'Ground' }))
  expect(screen.getByRole('heading', { name: 'Barely works against' })).toBeInTheDocument()
  expect(screen.getByText('×0.39', { exact: true })).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'See moves of this type' })).toHaveAttribute(
    'href',
    '/pokedex/moves?type=ground',
  )
  await user.click(screen.getByRole('button', { name: 'Clear' }))
  expect(screen.queryByRole('link', { name: 'See moves of this type' })).toBeNull()
  await user.click(screen.getByRole('radio', { name: 'Defending' }))
  expect(screen.getByText('Grass ×2.56')).toBeInTheDocument()
})

it('provides a keyboard path to a matrix matchup and restores focus when it closes', async () => {
  renderApp('/pokedex/types?mode=full', { layout: 'desktop' })
  const user = userEvent.setup()
  const table = screen.getByRole('table', { name: 'Full chart' })

  expect(within(table).getAllByRole('row')).toHaveLength(19)
  expect(within(table).getAllByRole('columnheader')).toHaveLength(19)
  expect(within(table).getAllByRole('button')).toHaveLength(324)

  const first = within(table).getByRole('button', { name: 'Normal attacks Normal for ×1 damage' })
  first.focus()
  await user.keyboard('{ArrowRight}{ArrowDown}{Enter}')
  const details = screen.getByRole('dialog', { name: 'Type matchup' })
  expect(details).toHaveTextContent('Fire attacks Fire for ×0.63 damage')
  await user.keyboard('{Escape}')
  expect(
    within(table).getByRole('button', { name: 'Fire attacks Fire for ×0.63 damage' }),
  ).toHaveFocus()
})

it('localizes the matchup number in European Portuguese', () => {
  const languages = new LanguageStore(new MemoryStorage(), ['pt-PT'])
  renderApp('/pokedex/types?types=water,ground', { languages })

  expect(screen.getByText('Planta ×2,56')).toBeInTheDocument()
})
