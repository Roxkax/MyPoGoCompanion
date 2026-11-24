import { expect, test } from '@playwright/test'

test('a dual typing combines matchups and opens a filtered Pokédex', async ({ page }) => {
  await page.goto('/pokedex/types?types=water,ground')
  await expect(page.getByText('Grass ×2.56')).toBeVisible()
  await expect(page.getByText('Electric ×0.63')).toBeVisible()
  await page.getByRole('link', { name: 'See Pokémon with this typing' }).click()
  await expect(page).toHaveURL(/\/pokedex\?type=water%2Cground$/)
  await expect(page.getByRole('searchbox', { name: 'Search the Pokédex' })).toBeVisible()
  await page.goBack()
  await expect(page.getByText('Grass ×2.56')).toBeVisible()
})

test('attacking mode replaces the selected type and opens filtered Moves', async ({ page }) => {
  await page.goto('/pokedex/types')
  await page.getByRole('radio', { name: 'Attacking' }).click()
  await page.getByRole('button', { name: 'Fire', exact: true }).click()
  await page.getByRole('button', { name: 'Ground', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Fire', exact: true })).toHaveAttribute('aria-pressed', 'false')
  await expect(page.getByRole('heading', { name: 'Barely works against ×0.39' })).toBeVisible()
  await page.getByRole('link', { name: 'See moves of this type' }).click()
  await expect(page).toHaveURL(/\/pokedex\/moves\?.*type=ground/)
  await expect(page.getByRole('searchbox', { name: 'Search moves' })).toBeVisible()
})

test('the whole matrix fits its scroll area and opens cell details', async ({ page }, info) => {
  const desktop = info.project.name.startsWith('desktop')
  await page.goto('/pokedex/types?types=water,ground')
  if (!desktop) await page.getByRole('radio', { name: 'Full chart' }).click()
  const table = page.getByRole('table', { name: 'Full chart' })
  await expect(table.getByRole('row')).toHaveCount(19)
  await expect(table.getByRole('columnheader')).toHaveCount(19)
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
    page.viewportSize()!.width,
  )

  const cell = table.getByRole('button', { name: 'Dragon attacks Fairy for ×0.39 damage' })
  await cell.click()
  const dialog = page.getByRole('dialog', { name: 'Type matchup' })
  await expect(dialog).toContainText('Dragon attacks Fairy for ×0.39 damage')
  await dialog.getByRole('button', { name: 'Close' }).click()
  await expect(dialog).toBeHidden()
})
