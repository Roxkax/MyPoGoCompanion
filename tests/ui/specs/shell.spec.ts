import { expect, test, type Page } from '@playwright/test'

// The app shell (E01-F03): layouts, addresses, Back, search and Ask, page not found. Behaviour checks only: on QA the
// version and data are real, so no fixed values are expected. Opening the app, going through the tabs and the theme
// are covered by the smoke journey (journey.spec.ts).

const isDesktop = (name: string) => name.startsWith('desktop')

async function noSidewaysOverflow(page: Page) {
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  )
  expect(overflow).toBeLessThanOrEqual(0)
}

test('nothing overflows sideways on any kind of screen', async ({ page }) => {
  for (const path of [
    '/',
    '/pokedex',
    '/pokedex/garchomp',
    '/battle/tiers/great-league',
    '/settings',
    '/nope',
  ]) {
    await page.goto(path)
    await expect(page.getByRole('main')).toBeVisible()
    await noSidewaysOverflow(page)
  }
})

test('an unreachable API shows a way to try again', async ({ page }) => {
  await page.route('**/health', (route) => route.abort('connectionrefused'))
  await page.goto('/')

  await expect(page.getByRole('status')).toContainText('Can’t reach the API', { timeout: 30_000 })
  await page.unroute('**/health')
  await page.getByRole('button', { name: 'Try again' }).click()
  await expect(page.getByRole('status')).toContainText('Connected to the API', { timeout: 30_000 })
})

test('the layout for the screen size: tab bar on a phone, sidebar and top bar on a desktop', async ({
  page,
}, testInfo) => {
  await page.goto('/battle/raids/kyogre')

  if (isDesktop(testInfo.project.name)) {
    const sidebar = page.getByRole('complementary')
    await expect(sidebar.getByRole('link', { name: 'Battle', exact: true })).toHaveAttribute(
      'aria-current',
      'page',
    )
    await expect(sidebar.getByRole('link', { name: 'Tier lists' })).toBeVisible()
    await expect(
      page.getByRole('banner').getByRole('button', { name: /Search Pokémon/ }),
    ).toBeVisible()
    await expect(page.getByRole('navigation', { name: 'Tabs' })).toHaveCount(0)
  } else {
    const tabs = page.getByRole('navigation', { name: 'Tabs' })
    await expect(tabs).toBeVisible()
    await expect(tabs.getByRole('link', { name: 'Battle' })).toHaveAttribute('aria-current', 'page')
    // The tab bar stays at the bottom of the screen.
    const box = (await tabs.boundingBox())!
    expect(box.y + box.height).toBeCloseTo(page.viewportSize()!.height, 0)
    await expect(page.getByRole('complementary')).toHaveCount(0)
  }
  await expect(page.getByRole('main')).toContainText('Raid boss')
  await expect(page.getByRole('main')).toContainText('kyogre')
})

test('every address opens its screen directly, also after a reload', async ({ page }) => {
  for (const [path, screen] of [
    ['/calendar/community-day-gible-2026-11-07', 'Event details'],
    ['/pokedex/moves/earth-power', 'Move'],
    ['/battle/teams?boss=kyogre', 'Best team'],
    ['/filters/builder', 'Filter builder'],
  ] as const) {
    await page.goto(path)
    await expect(page.getByRole('region', { name: screen, exact: true })).toBeVisible()
    await page.reload()
    await expect(page.getByRole('region', { name: screen, exact: true })).toBeVisible()
  }
})

test('a detail screen goes back to its parent screen', async ({ page }) => {
  await page.goto('/pokedex/moves/earth-power')

  await page.getByRole('link', { name: 'Moves', exact: true }).first().click()

  await expect(page).toHaveURL(/\/pokedex\/moves$/)
})

test('the section switcher changes section', async ({ page }) => {
  await page.goto('/battle/raids')
  const sections = page.getByRole('navigation', { name: 'Battle sections' })

  await sections.getByRole('link', { name: 'Tier lists' }).click()

  await expect(page).toHaveURL(/\/battle\/tiers\/great-league$/)
  await expect(sections.getByRole('link', { name: 'Tier lists' })).toHaveAttribute(
    'aria-current',
    'page',
  )
})

test("Ask opens over the page; the browser's Back closes it and stays on the page", async ({
  page,
}) => {
  await page.goto('/pokedex')

  await page.getByRole('button', { name: 'Ask' }).first().click()
  const panel = page.getByRole('dialog', { name: 'Ask' })
  await expect(panel).toBeVisible()
  await expect(panel.getByRole('region', { name: 'Ask the AI assistant' })).toBeVisible()
  await expect(page).toHaveURL(/\/pokedex$/)

  await page.goBack()

  await expect(panel).toBeHidden()
  await expect(page).toHaveURL(/\/pokedex$/)
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Pokédex')
})

test('search opens from the search bar and with Ctrl K; Escape closes it', async ({
  page,
}, testInfo) => {
  await page.goto(isDesktop(testInfo.project.name) ? '/battle/raids' : '/')

  await page.getByRole('button', { name: /Search Pokémon/ }).click()
  const search = page.getByRole('dialog', { name: 'Search' })
  await expect(search).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(search).toBeHidden()

  await page.keyboard.press('Control+k')
  await expect(search).toBeVisible()
  await search.getByRole('button', { name: 'Close' }).click()
  await expect(search).toBeHidden()
})

test('an unknown address and the admin screens (signed out) show "Page not found"', async ({
  page,
}) => {
  for (const path of ['/no/such/page', '/admin']) {
    await page.goto(path)
    await expect(page.getByRole('region', { name: 'Page not found' })).toBeVisible()
  }
  await page.getByRole('link', { name: 'Go to Today' }).click()
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Today')
})

test('Settings has no tab bar on a phone and goes back where it came from', async ({
  page,
}, testInfo) => {
  test.skip(isDesktop(testInfo.project.name), 'phone only')
  await page.goto('/filters')
  await page.getByRole('button', { name: 'Profile' }).click()
  await page.getByRole('menuitem', { name: 'Settings' }).click()

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Settings')
  await expect(page.getByRole('navigation', { name: 'Tabs' })).toHaveCount(0)
  await page.getByRole('button', { name: 'Back' }).click()
  await expect(page).toHaveURL(/\/filters$/)
})

test('a dark choice in Settings shows dark from the first paint of the next visit', async ({
  page,
}) => {
  await page.goto('/settings')
  await page.getByRole('radio', { name: 'Dark' }).click()

  await page.goto('/pokedex')
  // public/theme.js marks the page before the app's scripts run.
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
  const colour = await page.evaluate(() => getComputedStyle(document.body).backgroundColor)
  expect(colour).toBe('rgb(13, 21, 20)')
})
