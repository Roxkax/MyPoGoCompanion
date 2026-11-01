import { expect, test, type Page } from '@playwright/test'

// Languages (E01-F04): English and Português (Portugal). The choice is kept on the device and, signed in, with the
// account, whose language wins at sign-in (decision 2026-11-01). The account's answers are stood in for (a real
// Google account can't be used in a test); everything else is the real app.

const isDesktop = (name: string) => name.startsWith('desktop')

async function chooseLanguage(page: Page, current: 'Language' | 'Idioma', name: string) {
  await page.goto('/settings')
  await page.getByRole('radiogroup', { name: current }).getByRole('radio', { name }).click()
}

/** The tab names in the phone's tab bar or the desktop sidebar. */
function mainNavigation(page: Page, desktop: boolean) {
  return desktop
    ? page.getByRole('navigation', { name: /^(Main|Principal)$/ })
    : page.getByRole('navigation', { name: /^(Tabs|Separadores)$/ })
}

test('Settings → Português (Portugal) changes the whole app at once and is kept after a reload', async ({
  page,
}, testInfo) => {
  const desktop = isDesktop(testInfo.project.name)
  await chooseLanguage(page, 'Language', 'Português (Portugal)')

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Definições')
  await expect(page.locator('html')).toHaveAttribute('lang', 'pt-PT')
  await expect(page).toHaveTitle(/^Definições · /)
  await expect(page.getByRole('radio', { name: 'Seguir o dispositivo' })).toBeVisible()

  await page.goto('/collection')
  await expect(mainNavigation(page, desktop)).toContainText('Coleção')
  await expect(page.getByRole('heading', { name: 'A tua coleção fica aqui' })).toBeVisible()

  await page.reload()
  await expect(page.locator('html')).toHaveAttribute('lang', 'pt-PT')
  await expect(mainNavigation(page, desktop)).toContainText('Hoje')

  await chooseLanguage(page, 'Idioma', 'English')
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Settings')
  await expect(page.locator('html')).toHaveAttribute('lang', 'en')
})

test('Today’s date is written the way each language writes it', async ({ page }, testInfo) => {
  const desktop = isDesktop(testInfo.project.name)
  await page.goto('/')
  const date = desktop ? page.locator('.page-subtitle') : page.locator('.tab-subtitle')
  // "Tuesday 27 October [2026]"
  await expect(date).toHaveText(
    desktop ? /^[A-Z][a-z]+day \d{1,2} [A-Z][a-z]+ \d{4}$/ : /^[A-Z][a-z]+day \d{1,2} [A-Z][a-z]+$/,
  )

  await chooseLanguage(page, 'Language', 'Português (Portugal)')
  await page.goto('/')
  // "Terça-feira, 27 de outubro [de 2026]"
  await expect(date).toHaveText(
    desktop
      ? /^(\p{Lu}[\p{Ll}-]+), \d{1,2} de \p{Ll}+ de \d{4}$/u
      : /^(\p{Lu}[\p{Ll}-]+), \d{1,2} de \p{Ll}+$/u,
  )
})

test.describe('a browser set to Portuguese', () => {
  test.use({ locale: 'pt-PT' })

  test('opens the app in Português (Portugal) on the first visit', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Hoje')
    await expect(page.locator('html')).toHaveAttribute('lang', 'pt-PT')
  })
})

test.describe('signed in', () => {
  const owner = {
    id: '0190e0e0-0000-7000-8000-000000000001',
    email: 'owner@example.test',
    name: 'Test Owner',
    pictureUrl: null,
    role: 'SuperAdmin',
    theme: 'Device',
  }

  /** A returning visitor whose account is set to `language`; records what the app saves. */
  async function returningVisitor(page: Page, language: string) {
    const saved: unknown[] = []
    await page.route('**/auth/refresh', (route) =>
      route.fulfill({
        json: {
          accessToken: 'access',
          accessTokenExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
          refreshToken: 'r'.repeat(43),
          user: { ...owner, language },
        },
      }),
    )
    await page.route('**/me/language', (route) => {
      saved.push({
        method: route.request().method(),
        auth: route.request().headers().authorization,
        body: route.request().postDataJSON(),
      })
      return route.fulfill({ status: 204 })
    })
    await page.addInitScript(() => localStorage.setItem('mpgc.refreshToken', 'r'.repeat(43)))
    return saved
  }

  test('the account’s language wins on this device', async ({ page }) => {
    await returningVisitor(page, 'pt-PT')
    await page.goto('/')

    await expect(page.getByRole('button', { name: 'Conta: Test Owner' })).toBeVisible()
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Hoje')
    expect(await page.evaluate(() => localStorage.getItem('mpgc.language'))).toBe('pt-PT')
  })

  test('a change in Settings is saved with the account', async ({ page }) => {
    const saved = await returningVisitor(page, 'en')
    // Signed in once the app has swapped the stored refresh token (the phone's Settings has no account button).
    await Promise.all([page.waitForResponse('**/auth/refresh'), page.goto('/settings')])

    await page.getByRole('radio', { name: 'Português (Portugal)' }).click()

    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Definições')
    await expect
      .poll(() => saved)
      .toEqual([{ method: 'PUT', auth: 'Bearer access', body: { language: 'pt-PT' } }])
  })
})
