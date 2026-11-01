import { expect, test, type Page } from '@playwright/test'

// The smoke journey (E00-F07, extended with the app shell in E01-F03): what a visitor does first, as one test. On QA
// the data and version are real, so it checks behaviour, not fixed values: open the app, go through the tabs, follow
// the device's theme, then pick a theme in Settings.

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

test('smoke journey: open the app, go through the tabs, switch the theme', async ({
  page,
}, testInfo) => {
  const startTheme = testInfo.project.name.endsWith('dark') ? 'dark' : 'light'
  const otherTheme = startTheme === 'dark' ? 'light' : 'dark'
  const desktop = testInfo.project.name.startsWith('desktop')

  await test.step('open the app on Today', async () => {
    await page.goto('/')
    await expect(page).toHaveTitle(/MyPoGoCompanion$/)
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Today')
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

  await test.step(`go through the tabs (${desktop ? 'sidebar' : 'tab bar'})`, async () => {
    const nav = desktop
      ? page.getByRole('complementary').getByRole('navigation', { name: 'Main' })
      : page.getByRole('navigation', { name: 'Tabs' })
    for (const [tab, path, title] of [
      ['Pokédex', '/pokedex', 'Pokédex'],
      ['Battle', '/battle/raids', 'Battle'],
      ['Filters', '/filters', 'Filters'],
      ['Collection', '/collection', 'Collection'],
      ['Today', '/', 'Today'],
    ] as const) {
      await nav.getByRole('link', { name: tab, exact: true }).click()
      await expect(page).toHaveURL(new RegExp(`${path}$`))
      await expect(page.getByRole('heading', { level: 1 })).toHaveText(title)
      await expect(nav.getByRole('link', { name: tab, exact: true })).toHaveAttribute(
        'aria-current',
        'page',
      )
    }
  })

  await test.step(`pick ${otherTheme} in Settings: it wins over the device and stays after a reload`, async () => {
    if (desktop) {
      await page.getByRole('complementary').getByRole('link', { name: 'Settings' }).click()
    } else {
      await page.getByRole('button', { name: 'Profile' }).click()
      await page.getByRole('menuitem', { name: 'Settings' }).click()
    }
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Settings')
    const theme = page.getByRole('radiogroup', { name: 'Theme' })
    await expect(theme.getByRole('radio', { name: 'Follow device' })).toBeChecked()

    await theme.getByRole('radio', { name: otherTheme === 'dark' ? 'Dark' : 'Light' }).click()
    await expectTheme(page, otherTheme)
    await page.reload()
    await expectTheme(page, otherTheme)
    await expect(
      theme.getByRole('radio', { name: otherTheme === 'dark' ? 'Dark' : 'Light' }),
    ).toBeChecked()
  })

  await test.step('back to Follow device: the device decides again', async () => {
    await page
      .getByRole('radiogroup', { name: 'Theme' })
      .getByRole('radio', { name: 'Follow device' })
      .click()
    await expectTheme(page, startTheme)
  })
})
