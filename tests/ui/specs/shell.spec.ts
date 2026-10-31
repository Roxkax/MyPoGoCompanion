import { expect, test } from '@playwright/test'

// Behaviour checks only: on QA the version and data are real, so no fixed values are expected. Opening the app,
// reaching the API and following the device theme are covered by the smoke journey (journey.spec.ts).

test('nothing overflows sideways', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('status')).not.toContainText('Checking', { timeout: 30_000 })

  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  )
  expect(overflow).toBeLessThanOrEqual(0)
})

test('an unreachable API shows a way to try again', async ({ page }) => {
  await page.route('**/health', (route) => route.abort('connectionrefused'))
  await page.goto('/')

  await expect(page.getByRole('status')).toContainText('Can’t reach the API', { timeout: 30_000 })
  await page.unroute('**/health')
  await page.getByRole('button', { name: 'Try again' }).click()
  await expect(page.getByRole('status')).toContainText('Connected to the API', { timeout: 30_000 })
})
