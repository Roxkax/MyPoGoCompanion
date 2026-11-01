import { expect, test, type Page } from '@playwright/test'

// The first smoke journey (E00-F07): what a visitor does first, as one test. On QA the data and version are real,
// so it checks behaviour, not fixed values. It grows with the app: the tab bar and the in-app theme choice
// (Settings: follow device / light / dark) arrive with the app shell (E01-F03), which adds those steps here.

/** How light the page background is, from 0 (black) to 1 (white). */
async function backgroundLightness(page: Page): Promise<number> {
  const background = await page.evaluate(() => getComputedStyle(document.body).backgroundColor)
  const [r = 0, g = 0, b = 0] = background.match(/\d+/g)!.map(Number)
  return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255
}

async function expectTheme(page: Page, theme: 'light' | 'dark') {
  // The background changes colour with a short transition at most, so poll rather than read once.
  if (theme === 'dark') {
    await expect.poll(() => backgroundLightness(page)).toBeLessThan(0.2)
  } else {
    await expect.poll(() => backgroundLightness(page)).toBeGreaterThan(0.8)
  }
}

/** Which copy of the app the tests expect: "qa" in the QA deploy, "local" by default (web/src/environment.ts). */
const expectedEnv = process.env.EXPECTED_ENV ?? 'local'

test('smoke journey: open the app, then switch the theme', async ({ page }, testInfo) => {
  const startTheme = testInfo.project.name.endsWith('dark') ? 'dark' : 'light'
  const otherTheme = startTheme === 'dark' ? 'light' : 'dark'

  await test.step('open the app', async () => {
    await page.goto('/')
    await expect(page).toHaveTitle(/MyPoGoCompanion$/)
    await expect(page.getByRole('banner')).toContainText('MyPoGoCompanion')
    await expect(page.getByRole('main').getByRole('heading', { level: 1 })).toBeVisible()
    await expect(page.getByRole('contentinfo')).toContainText('Unofficial fan project')
  })

  await test.step(`it says which copy of the app this is (${expectedEnv})`, async () => {
    if (expectedEnv === 'production') {
      await expect(page).toHaveTitle('MyPoGoCompanion')
      await expect(page.getByRole('note')).toHaveCount(0)
    } else {
      const label = expectedEnv === 'qa' ? 'QA' : 'Local'
      await expect(page).toHaveTitle(`${label} · MyPoGoCompanion`)
      await expect(page.getByRole('note', { name: `${label} environment` })).toBeVisible()
      await expect(page.locator('html')).toHaveAttribute('data-env', expectedEnv)
    }
  })

  await test.step('the app reaches the API and its database', async () => {
    const status = page.getByRole('status')
    await expect(status).toContainText('Connected to the API', { timeout: 30_000 })
    await expect(status).toContainText(/Version \S+ · database working/)
  })

  await test.step(`it starts in the device's theme (${startTheme})`, async () => {
    await expectTheme(page, startTheme)
  })

  await test.step(`the device switches to ${otherTheme}: the app follows without a reload`, async () => {
    await page.emulateMedia({ colorScheme: otherTheme })
    await expectTheme(page, otherTheme)
    // Still the same page and still connected: nothing was reloaded or lost.
    await expect(page.getByRole('status')).toContainText('Connected to the API')
  })

  await test.step(`and back to ${startTheme}`, async () => {
    await page.emulateMedia({ colorScheme: startTheme })
    await expectTheme(page, startTheme)
  })
})
