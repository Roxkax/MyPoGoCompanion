import { expect, test } from '@playwright/test'

// Behaviour checks only: on QA the version and data are real, so no fixed values are expected.

test('the app shell loads and reaches the API', async ({ page }) => {
  await page.goto('/')

  await expect(page).toHaveTitle('MyPoGoCompanion')
  await expect(page.getByRole('banner')).toContainText('MyPoGoCompanion')
  const status = page.getByRole('status')
  await expect(status).toContainText('Connected to the API', { timeout: 30_000 })
  await expect(status).toContainText(/Version \S+ · database working/)
})

test('it follows the device theme', async ({ page }, testInfo) => {
  await page.goto('/')

  const background = await page.evaluate(() => getComputedStyle(document.body).backgroundColor)
  const [r = 0, g = 0, b = 0] = background.match(/\d+/g)!.map(Number)
  const luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255
  if (testInfo.project.name.endsWith('dark')) {
    expect(luminance).toBeLessThan(0.2)
  } else {
    expect(luminance).toBeGreaterThan(0.8)
  }
})

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
