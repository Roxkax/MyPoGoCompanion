import { expect, test, type Page } from '@playwright/test'

// The out-of-date warning (E01-F07): an orange bar where data that stopped updating appears. The API decides
// (GET /freshness); on QA and locally the jobs haven't run yet, so most tests answer for the API with old data. The
// real API is checked for its shape and that the app agrees with it.

const liveText = /Raids and events haven’t updated in 12 days\. Some may have changed in the game\./
const gameText = /Game data hasn’t updated in 4 days\. Recent changes in the game may be missing\./

const old = (days: number, limitDays: number) => ({
  lastUpdatedAt: '2026-10-20T18:00:00+00:00',
  daysSinceUpdate: days,
  outOfDate: true,
  limitDays,
})

async function answerOld(page: Page) {
  await page.route('**/freshness', (route) =>
    route.fulfill({ json: { gameData: old(4, 3), liveData: old(12, 9) } }),
  )
}

const warnings = (page: Page) => page.getByRole('main').locator('.stale-warning')

test('the real API says how fresh the data is, and the app agrees', async ({ page }) => {
  // Passed through untouched; read on the way so the test knows what the app was told.
  let body: Record<string, { outOfDate: boolean; limitDays: number }> | undefined
  await page.route('**/freshness', async (route) => {
    const response = await route.fetch()
    expect(response.ok()).toBe(true)
    body = (await response.json()) as typeof body
    await route.fulfill({ response })
  })
  await page.goto('/battle/raids')
  await expect.poll(() => body).toBeDefined()

  for (const kind of ['gameData', 'liveData']) {
    expect(typeof body![kind]!.outOfDate).toBe('boolean')
    expect(body![kind]!.limitDays).toBeGreaterThan(0)
  }
  const expected = [body!.liveData!.outOfDate, body!.gameData!.outOfDate].filter(Boolean).length
  await expect(page.getByRole('main')).toContainText('Raid')
  await expect(warnings(page)).toHaveCount(expected)
})

test('old data: the warning sits at the top of the screens that show it', async ({ page }) => {
  await answerOld(page)

  await page.goto('/')
  await expect(warnings(page)).toHaveCount(1)
  await expect(warnings(page)).toHaveText(liveText)

  await page.goto('/pokedex')
  await expect(warnings(page)).toHaveCount(1)
  await expect(warnings(page)).toHaveText(gameText)

  await page.goto('/battle/raids/kyogre')
  await expect(warnings(page)).toHaveCount(2)
  await expect(warnings(page).first()).toHaveText(liveText)
  await expect(warnings(page).last()).toHaveText(gameText)

  // Above the screen's own content, inside the page, orange on a soft orange background, nothing sideways.
  const layout = await page.evaluate(() => {
    const bar = document.querySelector('main .stale-warning')!
    const box = bar.getBoundingClientRect()
    const content = bar.parentElement!.nextElementSibling!.getBoundingClientRect()
    const style = getComputedStyle(bar)
    return {
      above: box.bottom <= content.top,
      inside: box.left >= 0 && box.right <= document.documentElement.clientWidth,
      overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
      background: style.backgroundColor,
      color: style.color,
    }
  })
  expect(layout.above).toBe(true)
  expect(layout.inside).toBe(true)
  expect(layout.overflow).toBeLessThanOrEqual(0)
  expect(layout.background).not.toBe(layout.color)
  expect(layout.background).not.toBe('rgba(0, 0, 0, 0)')

  await page.goto('/settings')
  await expect(page.getByRole('main')).toBeVisible()
  await expect(warnings(page)).toHaveCount(0)
})

test('old data, in Portuguese', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('mpgc.language', 'pt-PT'))
  await answerOld(page)

  await page.goto('/battle/teams')
  await expect(warnings(page).first()).toHaveText(
    'Os reides e eventos não são atualizados há 12 dias. Alguns podem ter mudado no jogo.',
  )
  await expect(warnings(page).last()).toHaveText(
    'Os dados do jogo não são atualizados há 4 dias. Podem faltar mudanças recentes no jogo.',
  )
})

test('QA and local: ?stale= pretends the data is old until ?stale=off', async ({ page }) => {
  await page.goto('/')
  test.skip((await page.locator('html[data-env="production"]').count()) > 0, 'Not in production')

  await page.goto('/?stale=both')
  await expect(warnings(page)).toHaveText([liveText])

  // It holds while moving around the app, then ends.
  await page.goto('/battle/raids')
  await expect(warnings(page)).toHaveText([liveText, gameText])
  await page.goto('/pokedex?stale=off')
  await expect(page.getByRole('main')).toBeVisible()
  await expect(warnings(page)).toHaveCount(0)
})

test('the design gallery shows both warnings', async ({ page }) => {
  await page.goto('/design')
  test.skip(
    (await page.getByRole('heading', { name: 'Page not found' }).count()) > 0,
    'No gallery in production',
  )
  const gallery = page.getByRole('region', { name: 'Out-of-date warnings' })
  await expect(gallery.getByText(liveText)).toBeVisible()
  await expect(gallery.getByText(gameText)).toBeVisible()
})
