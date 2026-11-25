import { expect, test } from '@playwright/test'

test('builds and edits an English storage search from conditions', async ({ page, context }, info) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write'])
  await page.goto('/filters/builder')

  const desktop = info.project.name.startsWith('desktop')
  const add = page.getByRole('button', { name: 'Add a condition' })
  const search = page.getByRole('searchbox', { name: 'Search conditions' })
  const expression = page.getByRole('textbox', { name: /^Search string/ })
  if (!desktop) {
    await add.click()
    await expect(page.getByRole('dialog', { name: 'Add a condition' })).toBeVisible()
  }

  await search.fill('water')
  await expect(page.getByRole('button', { name: /Choose one or more of the 18 types/ })).toBeVisible()
  await expect(page.getByRole('button', { name: /Names, numbers, families and regions/ })).toHaveCount(0)
  await page.getByRole('button', { name: /Choose one or more of the 18 types/ }).click()
  await page.getByRole('checkbox', { name: 'Fire', exact: true }).check()
  await page.getByRole('checkbox', { name: 'Water', exact: true }).check()
  await page.getByRole('button', { name: 'Apply condition' }).click()
  await expect(expression).toHaveValue('fire,water')

  if (!desktop) await add.click()
  await search.fill('shiny')
  await page.getByRole('button', { name: /Shiny, lucky, Shadow/ }).click()
  await page.getByRole('checkbox', { name: 'Exclude values I add' }).check()
  await page.getByRole('checkbox', { name: 'Shiny', exact: true }).check()
  await page.getByRole('button', { name: 'Apply condition' }).click()
  await expect(expression).toHaveValue('fire,water&!shiny')

  await expression.fill('fire;water|!shiny&cp-1500')
  await expect(page.getByRole('button', { name: 'Edit Types: Fire or Water' })).toBeVisible()
  await expect(page.getByRole('button', { name: /Edit CP & HP/ })).toBeVisible()
  await page.getByRole('button', { name: 'Copy', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Copied', exact: true })).toBeVisible()
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe('fire;water|!shiny&cp-1500')

  await page.getByRole('button', { name: /Edit CP & HP/ }).click()
  await page.getByRole('button', { name: /Remove CP up to/ }).click()
  await page.getByRole('spinbutton', { name: 'Maximum' }).fill('2500')
  await page.getByRole('button', { name: 'Add value' }).click()
  await page.getByRole('button', { name: 'Apply condition' }).click()
  await expect(expression).toHaveValue('fire,water&!shiny&cp-2500')
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(page.viewportSize()!.width)
})

test('checks invalid ranges and splits long alternatives without dropping the CP cap', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write'])
  await page.goto('/filters/builder')
  const expression = page.getByRole('textbox', { name: /^Search string/ })
  await expression.fill('443&cp1500-1000')
  await expect(page.getByText(/Check the separators or the range/)).toBeVisible()
  await expect(page.getByRole('button', { name: 'Copy', exact: true })).toHaveCount(0)

  const dexes = Array.from({ length: 950 }, (_, index) => String(index + 1)).join(',')
  await expression.fill(`${dexes}&cp-1500`)
  await expect(page.getByText(/Paste each part/)).toBeVisible()
  const copyParts = page.getByRole('button', { name: /^Copy · Part/ })
  const parts: string[] = []
  while (await copyParts.count()) {
    await copyParts.first().click()
    await expect(page.getByRole('button', { name: /^Copied · Part/ })).toHaveCount(parts.length + 1)
    parts.push(await page.evaluate(() => navigator.clipboard.readText()))
  }
  expect(parts.length).toBeGreaterThan(1)
  expect(parts.every((part) => part.endsWith('&cp-1500') && part.length <= 1950)).toBe(true)
  expect(parts.map((part) => part.replace('&cp-1500', '')).join(',')).toBe(dexes)
})
