import { expect, test, type Page, type TestInfo } from '@playwright/test'
import type { components } from '../../../web/src/api/generated/schema'

// B3 Moves list (E03-F03). One journey uses the real API; fixed answers make sorting, missing raid data and
// probabilistic PvP effects reproducible on the dev seed and QA's changing game data alike.
type Move = components['schemas']['PageMove']
const isDesktop = (info: TestInfo) => info.project.name.startsWith('desktop')
const rows = (page: Page) => page.locator('[data-move]')
const row = (page: Page, id: string) => page.locator(`[data-move="${id}"]`)
const search = (page: Page) => page.getByRole('searchbox', { name: 'Search moves' })

const base: Move = {
  id: 'DRAGON_CLAW', nameEn: 'Dragon Claw', namePt: 'Garra de Dragão', type: 'dragon', kind: 'Charged',
  raidPower: 50, raidDurationMs: 1500, raidEnergy: -33, pvpPower: 80, pvpEnergy: -50, pvpTurns: null,
  pvpBuffChance: null, pvpSelfAttack: 0, pvpSelfDefense: 0, pvpOpponentAttack: 0, pvpOpponentDefense: 0,
}
const moves: Move[] = [
  { ...base, id: 'EARTH_POWER', nameEn: 'Earth Power', namePt: 'Poder da Terra', type: 'ground',
    raidPower: 100, raidDurationMs: 3500, raidEnergy: -50, pvpPower: 90,
    pvpBuffChance: 0.1, pvpOpponentDefense: -1 },
  base,
  { ...base, id: 'DRAGON_ENERGY', nameEn: 'Dragon Energy', namePt: 'Energia de Dragão',
    raidPower: 155, raidDurationMs: 3500, raidEnergy: -50, pvpPower: 90, pvpEnergy: -45 },
  { ...base, id: 'BREAKING_SWIPE', nameEn: 'Breaking Swipe', namePt: null,
    raidPower: 45, raidDurationMs: 1000, pvpPower: 50, pvpBuffChance: 1, pvpOpponentAttack: -1 },
  { ...base, id: 'DRACO_METEOR', nameEn: 'Draco Meteor', namePt: 'Meteoro do Dragão',
    raidPower: 150, raidDurationMs: 3500, raidEnergy: -100, pvpPower: 150, pvpEnergy: -65,
    pvpBuffChance: 1, pvpSelfAttack: -2 },
  { ...base, id: 'PVP_ONLY', nameEn: 'PvP Only', namePt: 'Só PvP', type: 'normal',
    raidPower: 0, raidDurationMs: 0, raidEnergy: 0, pvpPower: 35, pvpEnergy: -35 },
  { ...base, id: 'DRAGON_TAIL', nameEn: 'Dragon Tail', namePt: 'Cauda do Dragão', kind: 'Fast',
    raidPower: 14, raidDurationMs: 1000, raidEnergy: 8, pvpPower: 9, pvpEnergy: 12, pvpTurns: 3 },
  { ...base, id: 'EMBER', nameEn: 'Ember', namePt: 'Brasa', type: 'fire', kind: 'Fast',
    raidPower: 10, raidDurationMs: 1000, raidEnergy: 10, pvpPower: 7, pvpEnergy: 6, pvpTurns: 2 },
  { ...base, id: 'MUD_SHOT', nameEn: 'Mud Shot', namePt: 'Tiro de Lama', type: 'ground', kind: 'Fast',
    raidPower: 5, raidDurationMs: 500, raidEnergy: 7, pvpPower: 3, pvpEnergy: 8, pvpTurns: 2 },
]

async function mockMoves(page: Page) {
  await page.route('**/moves', (route) => route.request().isNavigationRequest()
    ? route.continue()
    : route.fulfill({ json: { moves } }))
}

async function openMoves(page: Page, query = '') {
  await page.goto(`/pokedex/moves${query}`)
  await expect(rows(page).first()).toBeVisible({ timeout: 30_000 })
}

async function ids(page: Page) {
  return rows(page).evaluateAll((items) => items.map((item) => item.getAttribute('data-move')))
}

async function selectType(page: Page, name: string) {
  await page.getByRole('button', { name: /^Type(?:\: .+)? ▾$/ }).click()
  const dialog = page.getByRole('dialog', { name: 'Type', exact: true })
  await dialog.getByRole('button', { name, exact: true }).click()
  if (await dialog.isVisible()) await page.keyboard.press('Escape')
}

async function sortBy(page: Page, info: TestInfo, label: string) {
  if (isDesktop(info)) {
    await page.getByRole('columnheader').getByRole('button', { name: label, exact: true }).click()
  } else {
    await page.getByRole('button', { name: /^Sort:/ }).click()
    const dialog = page.getByRole('dialog', { name: 'Sort by' })
    await dialog.getByRole('button', { name: new RegExp(`^${label}(?:$| )`) }).click()
    if (await dialog.isVisible()) await page.keyboard.press('Escape')
  }
}

async function numericColumn(page: Page, stat: string) {
  const cells = rows(page).locator(`[data-stat="${stat}"]`)
  const values = (await cells.allTextContents()).map((text) => Number(text.trim()))
  expect(values.length, 'assert multiple rows, never a single selected cell').toBeGreaterThan(1)
  expect(values.every(Number.isFinite)).toBe(true)
  return values
}

test('the real Moves API populates both kinds and serves a reusable cached answer', async ({ page }) => {
  const responsePromise = page.waitForResponse((response) =>
    new URL(response.url()).pathname === '/moves' && !response.request().isNavigationRequest())
  await openMoves(page)
  const response = await responsePromise
  expect(response.status()).toBe(200)
  // Read through Playwright's request context: Chromium can release a large compressed response body after React
  // commits the navigation, while this independent request remains available for contract assertions.
  const apiResponse = await page.request.get(response.url())
  expect(apiResponse.status()).toBe(200)
  const data = (await apiResponse.json()) as { moves: Move[] }
  const charged = data.moves.filter((move) => move.kind === 'Charged')
  const fast = data.moves.filter((move) => move.kind === 'Fast')
  expect(charged.length).toBeGreaterThan(1)
  expect(fast.length).toBeGreaterThan(1)
  await expect(rows(page)).toHaveCount(charged.length)
  await page.getByRole('radio', { name: /^Fast/ }).click()
  await expect(rows(page)).toHaveCount(fast.length)
  await expect(row(page, fast[0]!.id)).toContainText(fast[0]!.nameEn)
  expect(apiResponse.headers()['cache-control']).toBe('public, max-age=3600')
  const etag = apiResponse.headers()['etag']
  expect(etag).toMatch(/^"[0-9a-f]{32}"$/)
  const unchanged = await page.request.get(response.url(), { headers: { 'If-None-Match': etag! } })
  expect(unchanged.status()).toBe(304)
})

test.describe('search, filters and numbers', () => {
  test.beforeEach(async ({ page }) => { await mockMoves(page) })

  test('Fast and Charged show the correct moves and keyboard navigation changes the kind', async ({ page }) => {
    await openMoves(page)
    await expect(rows(page)).toHaveCount(6)
    const charged = page.getByRole('radio', { name: /^Charged/ })
    await expect(charged).toBeChecked()
    await charged.focus()
    await page.keyboard.press('ArrowLeft')
    await expect(page.getByRole('radio', { name: /^Fast/ })).toBeChecked()
    await expect(rows(page)).toHaveCount(3)
    expect(await ids(page)).toEqual(['DRAGON_TAIL', 'EMBER', 'MUD_SHOT'])
    await expect(page).toHaveURL(/kind=fast/)
    await expect(row(page, 'DRAGON_TAIL').locator('[data-stat="raidEnergy"]')).toHaveText('+8')
    await page.getByRole('radio', { name: /^Charged/ }).click()
    await expect(row(page, 'DRAGON_CLAW').locator('[data-stat="raidEnergy"]')).toHaveText('−33')
  })

  test('type and search combine, survive reload, and the type filter can be removed', async ({ page }, info) => {
    await openMoves(page)
    await selectType(page, 'Dragon')
    await expect(rows(page)).toHaveCount(4)
    await expect(page).toHaveURL(/type=dragon/)
    await search(page).fill('claw')
    await expect(rows(page)).toHaveCount(1)
    await expect(row(page, 'DRAGON_CLAW')).toBeVisible()
    await expect(page).toHaveURL(/q=claw/)
    await page.reload()
    await expect(row(page, 'DRAGON_CLAW')).toBeVisible()
    await expect(search(page)).toHaveValue('claw')
    await search(page).fill('')
    if (isDesktop(info)) {
      await page.getByRole('button', { name: 'Type: Dragon ▾', exact: true }).click()
      await page.getByRole('dialog', { name: 'Type', exact: true })
        .getByRole('button', { name: 'All types', exact: true }).click()
    } else {
      await page.getByRole('button', { name: 'Remove Dragon', exact: true }).click()
    }
    await expect(rows(page)).toHaveCount(6)
    await expect(page).not.toHaveURL(/type=/)
  })

  test('sorting reads every numeric cell and reverses the displayed order', async ({ page }, info) => {
    await openMoves(page, '?type=dragon')
    const descending = await numericColumn(page, 'raidPower')
    expect(descending).toEqual([155, 150, 50, 45])
    await sortBy(page, info, 'Raid power')
    await expect(page).toHaveURL(/order=asc/)
    expect(await numericColumn(page, 'raidPower')).toEqual([45, 50, 150, 155])
    await sortBy(page, info, 'Raid power')
    expect(await numericColumn(page, 'raidPower')).toEqual([155, 150, 50, 45])
  })

  test('phone switches Raids to PvP; desktop shows both, including turns and effects', async ({ page }, info) => {
    await openMoves(page)
    if (isDesktop(info)) {
      await expect(page.getByRole('columnheader', { name: 'Raids', exact: true })).toBeVisible()
      await expect(page.getByRole('columnheader', { name: 'PvP (trainer battles)', exact: true })).toBeVisible()
      await expect(page.getByRole('radiogroup', { name: 'Battle stats' })).toHaveCount(0)
    } else {
      await expect(row(page, 'DRAGON_CLAW').locator('[data-stat="pvpPower"]')).toHaveCount(0)
      await page.getByRole('radio', { name: 'PvP', exact: true }).click()
      await expect(page).toHaveURL(/mode=pvp/)
      await expect(row(page, 'DRAGON_CLAW').locator('[data-stat="raidPower"]')).toHaveCount(0)
    }
    await expect(row(page, 'DRAGON_CLAW').locator('[data-stat="pvpPower"]')).toHaveText('80')
    await expect(row(page, 'DRAGON_CLAW').locator('[data-stat="pvpEnergy"]')).toHaveText('−50')
    await expect(row(page, 'DRACO_METEOR')).toContainText('Self Atk −2')
    await expect(row(page, 'EARTH_POWER')).toContainText('Foe Def −1')
    await expect(row(page, 'EARTH_POWER')).toContainText('10%')
    await expect(row(page, 'BREAKING_SWIPE')).toContainText('Foe Atk −1')
    if (isDesktop(info)) await sortBy(page, info, 'PvP power')
    let values = await numericColumn(page, 'pvpPower')
    expect(values).toEqual([...values].sort((a, b) => b - a))
    expect((await ids(page))[0]).toBe('DRACO_METEOR')
    await sortBy(page, info, 'PvP power')
    values = await numericColumn(page, 'pvpPower')
    expect(values).toEqual([...values].sort((a, b) => a - b))
    await sortBy(page, info, 'PvP power')
    expect(await numericColumn(page, 'pvpPower')).toEqual([...values].sort((a, b) => b - a))
    await page.getByRole('radio', { name: /^Fast/ }).click()
    await expect(row(page, 'DRAGON_TAIL').locator('[data-stat="pvpTurns"]')).toHaveText('3')
    await expect(row(page, 'DRAGON_TAIL').locator('[data-stat="pvpEnergy"]')).toHaveText('+12')
  })

  test('a move without raid numbers uses a missing value instead of zero stats', async ({ page }) => {
    await openMoves(page)
    for (const stat of ['raidPower', 'raidDurationMs', 'raidEnergy']) {
      await expect(row(page, 'PVP_ONLY').locator(`[data-stat="${stat}"]`)).toHaveText('—')
    }
  })

  test('an unmatched search explains the empty result and can be cleared', async ({ page }) => {
    await openMoves(page)
    await search(page).fill('no-such-move')
    await expect(rows(page)).toHaveCount(0)
    await expect(page.getByRole('heading', { name: 'No moves match', exact: true })).toBeVisible()
    await page.getByRole('button', { name: 'Clear search and filters' }).click()
    await expect(rows(page)).toHaveCount(6)
    await expect(search(page)).toHaveValue('')
  })

  test('Portuguese names, accent-free search, missing-name fallback and narrow layout', async ({ page }, info) => {
    await page.addInitScript(() => localStorage.setItem('mpgc.language', 'pt-PT'))
    if (!isDesktop(info)) await page.setViewportSize({ width: 320, height: 852 })
    await openMoves(page)
    await expect(page.locator('html')).toHaveAttribute('lang', 'pt-PT')
    await expect(row(page, 'DRAGON_CLAW')).toContainText('Garra de Dragão')
    await expect(row(page, 'BREAKING_SWIPE')).toContainText('Breaking Swipe')
    const localizedSearch = page.getByRole('searchbox')
    await localizedSearch.fill('garra de dragao')
    await expect(rows(page)).toHaveCount(1)
    await expect(row(page, 'DRAGON_CLAW')).toBeVisible()
    await localizedSearch.fill('Dragon Claw')
    await expect(row(page, 'DRAGON_CLAW')).toBeVisible()
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
    expect(overflow).toBeLessThanOrEqual(0)
  })
})
