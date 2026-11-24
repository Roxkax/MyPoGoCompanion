import { expect, test } from '@playwright/test'

test('the published keeper cards fit the screen and copy separate PvP parts', async ({ page, context }, info) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write'])
  await page.goto('/filters')

  const great = page.getByRole('region', { name: 'Great League' })
  const ultra = page.getByRole('region', { name: 'Ultra League' })
  await expect(great.getByRole('button', { name: /^Copy Part/ })).toHaveCount(2)
  await expect(ultra.getByRole('button', { name: /^Copy Part/ })).toHaveCount(3)
  await expect(page.getByRole('region', { name: 'Raid attackers' })).toBeVisible()
  await expect(page.getByRole('region', { name: 'Max Battle attackers' })).toBeVisible()
  await expect(page.getByRole('region', { name: 'Megas' })).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
    page.viewportSize()!.width,
  )

  const desktop = info.project.name.startsWith('desktop')
  if (!desktop) await page.getByText('How to use these in the game', { exact: true }).click()
  await expect(page.getByText('Copy a search string.', { exact: true })).toBeVisible()
  await expect(great.locator('.keeper-preview').first()).toBeVisible({ visible: desktop })

  await great.getByRole('button', { name: /^Copy Part 1/ }).click()
  await expect(great.getByRole('button', { name: /^Copied Part 1/ })).toBeVisible()
  const first = await page.evaluate(() => navigator.clipboard.readText())
  expect(first).toMatch(/&cp-1500$/)
  expect(first.length).toBeLessThanOrEqual(1950)
  await great.getByRole('button', { name: /^Copy Part 2/ }).click()
  await expect(great.getByRole('button', { name: /^Copied Part 2/ })).toBeVisible()
  const second = await page.evaluate(() => navigator.clipboard.readText())
  expect(second).toMatch(/&cp-1500$/)
  expect(second).not.toBe(first)
})

test('Max Battle and Mega variants copy different searches', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write'])
  await page.goto('/filters')
  const max = page.getByRole('region', { name: 'Max Battle attackers' })
  await max.getByRole('radio', { name: '15 only' }).click()
  await max.getByRole('button', { name: 'Copy', exact: true }).click()
  await expect(max.getByRole('button', { name: 'Copied', exact: true })).toBeVisible()
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe('maxmove1-&4attack')

  const mega = page.getByRole('region', { name: 'Megas' })
  await expect(mega.getByRole('radio', { name: 'Yes' })).toBeChecked()
  await mega.getByRole('button', { name: 'Copy', exact: true }).click()
  await expect(mega.getByRole('button', { name: 'Copied', exact: true })).toBeVisible()
  const including = await page.evaluate(() => navigator.clipboard.readText())
  await mega.getByRole('radio', { name: 'No' }).click()
  await mega.getByRole('button', { name: 'Copy', exact: true }).click()
  await expect(mega.getByRole('button', { name: 'Copied', exact: true })).toBeVisible()
  const excluding = await page.evaluate(() => navigator.clipboard.readText())
  expect(excluding).toMatch(/^mega0-,/)
  expect(including).toContain(`${excluding},`)
})

test('Safe to transfer explains when to use the filter and copies its search', async ({ page, context }, info) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write'])
  await page.goto('/filters')

  const transfer = page.getByRole('region', { name: 'Safe to transfer' })
  await expect(transfer.getByText(/Tag the results of all five keeper filters first/)).toBeVisible()
  await expect(transfer.getByText(/Check the search results before transferring/)).toBeVisible()
  await expect(transfer.locator('.keeper-preview')).toBeVisible({
    visible: info.project.name.startsWith('desktop'),
  })
  await transfer.getByRole('button', { name: 'Copy', exact: true }).click()
  await expect(transfer.getByRole('button', { name: 'Copied', exact: true })).toBeVisible()
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(
    '!#GL&!#UL&!#Raid&!#Max&!#Mega&!shiny&!lucky&!legendary&!mythical&!favorite&!4*&!costume',
  )
})
