import { expect, test, type APIRequestContext, type Page } from '@playwright/test'

// Installable app and offline screen (E01-F06): the manifest and service worker, the "You're offline" screen (H3),
// Settings → Install the app and the one-time install hint. The service worker and manifest exist only in a built copy
// (QA, or `npm run build && npm run preview` with BASE_URL=http://localhost:4173); on the dev server those tests skip.

const isDesktop = (name: string) => name.startsWith('desktop')

/** Whether this copy is built (has /sw.js); the dev server answers every address with the app page. */
async function isBuilt(request: APIRequestContext): Promise<boolean> {
  const response = await request.get('/sw.js')
  return response.ok() && (response.headers()['content-type'] ?? '').includes('javascript')
}

/** Chrome's "this app can be installed" announcement; the install window answers `outcome`. */
async function announceInstall(page: Page, outcome: 'accepted' | 'dismissed' = 'dismissed') {
  await page.evaluate((answer) => {
    const event = new Event('beforeinstallprompt', { cancelable: true })
    Object.assign(event, {
      prompt: async () => {
        ;(window as unknown as { installPrompted: number }).installPrompted =
          ((window as unknown as { installPrompted?: number }).installPrompted ?? 0) + 1
      },
      userChoice: Promise.resolve({ outcome: answer }),
    })
    window.dispatchEvent(event)
  }, outcome)
}

const offlineHeading = (page: Page) => page.getByRole('heading', { name: 'You’re offline' })

test('the app can be installed: manifest with its name, icons and full screen', async ({
  page,
  request,
}) => {
  test.skip(!(await isBuilt(request)), 'Needs a built copy (QA or vite preview)')
  await page.goto('/')

  const href = await page.locator('link[rel="manifest"]').getAttribute('href')
  expect(href).toBe('/manifest.webmanifest')
  const response = await request.get(href!)
  expect(response.ok()).toBe(true)
  const manifest = await response.json()
  expect(manifest.name).toContain('MyPoGoCompanion')
  expect(manifest.display).toBe('standalone')
  expect(manifest.start_url).toBe('/')
  expect(manifest.icons.length).toBeGreaterThanOrEqual(3)

  const appleIcon = await page.locator('link[rel="apple-touch-icon"]').getAttribute('href')
  for (const src of [...manifest.icons.map((icon: { src: string }) => icon.src), appleIcon]) {
    const icon = await request.get(src)
    expect(icon.ok(), src).toBe(true)
    expect(icon.headers()['content-type'], src).toContain('image/png')
  }
})

test('without a connection the saved copy opens with the offline screen, and comes back by itself', async ({
  page,
  context,
  request,
}) => {
  test.skip(!(await isBuilt(request)), 'Needs a built copy (QA or vite preview)')
  await page.goto('/')
  await page.evaluate(() => navigator.serviceWorker.ready)
  await page.reload()
  await expect(page.getByRole('main')).toBeVisible()

  await context.setOffline(true)
  await expect(offlineHeading(page)).toBeVisible()

  // Opened again without a connection: the service worker's copy, at any address.
  await page.goto('/pokedex')
  await expect(offlineHeading(page)).toBeVisible()
  await page.getByRole('button', { name: 'Try again' }).click()
  await expect(page.getByRole('status').filter({ hasText: 'Still no connection' })).toBeVisible()

  await context.setOffline(false)
  await expect(offlineHeading(page)).toHaveCount(0, { timeout: 30_000 })
  await expect(page.getByRole('main')).toBeVisible()
})

test('losing the connection shows the offline screen in place of the page', async ({
  page,
  context,
}, testInfo) => {
  await page.goto('/battle/raids')
  await expect(page.getByRole('main')).toBeVisible()

  await context.setOffline(true)
  await expect(offlineHeading(page)).toBeVisible()
  await expect(page.getByText(/needs a connection for now/)).toBeVisible()
  if (isDesktop(testInfo.project.name)) {
    await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible()
  } else {
    await expect(page.getByRole('navigation', { name: 'Tabs' })).toBeVisible()
  }
  await context.setOffline(false)
})

test('Settings → Install the app explains where the browser keeps it', async ({ page }, testInfo) => {
  await page.goto('/settings')
  const row = page.getByRole('button', { name: /Install the app/ })
  await expect(row).toContainText(
    isDesktop(testInfo.project.name) ? 'Add to your computer' : 'Add to home screen',
  )

  await row.click()
  const guide = page.getByRole('dialog', { name: 'Install MyPoGoCompanion' })
  await expect(guide).toBeVisible()
  await expect(guide).toContainText('Install app')
  await guide.getByRole('button', { name: 'Got it' }).click()
  await expect(guide).toHaveCount(0)
})

test('Settings → Install the app opens the browser’s own install window where there is one', async ({
  page,
}) => {
  await page.goto('/settings')
  await expect(page.getByRole('button', { name: /Install the app/ })).toBeVisible()
  await announceInstall(page)

  await page.getByRole('button', { name: /Install the app/ }).click()
  await expect
    .poll(() => page.evaluate(() => (window as unknown as { installPrompted?: number }).installPrompted))
    .toBe(1)
  await expect(page.getByRole('dialog')).toHaveCount(0)
})

test('the install hint shows on the phone from the second visit, and closing it is for good', async ({
  page,
}, testInfo) => {
  // One visit already made on this device; this one is the second.
  await page.addInitScript(() => {
    if (!localStorage.getItem('mpgc.visits')) {
      localStorage.setItem('mpgc.visits', '1')
    }
  })
  await page.goto('/')
  await expect(page.getByRole('main')).toBeVisible()
  await announceInstall(page)
  const hint = page.locator('aside.install-hint')

  if (isDesktop(testInfo.project.name)) {
    await expect(hint).toHaveCount(0)
    return
  }
  await expect(hint).toBeVisible()
  await expect(hint).toContainText('Install the app')
  await hint.getByRole('button', { name: 'Don’t show this again' }).click()
  await expect(hint).toHaveCount(0)

  await page.reload()
  await expect(page.getByRole('main')).toBeVisible()
  await announceInstall(page)
  await expect(page.getByRole('navigation', { name: 'Tabs' })).toBeVisible()
  await expect(hint).toHaveCount(0)
})

test('the install hint’s Install opens the browser’s install window', async ({ page }, testInfo) => {
  test.skip(isDesktop(testInfo.project.name), 'The hint is for phones')
  await page.addInitScript(() => localStorage.setItem('mpgc.visits', '5'))
  await page.goto('/')
  await expect(page.getByRole('main')).toBeVisible()
  await announceInstall(page, 'accepted')

  const hint = page.locator('aside.install-hint')
  await hint.getByRole('button', { name: 'Install', exact: true }).click()
  await expect
    .poll(() => page.evaluate(() => (window as unknown as { installPrompted?: number }).installPrompted))
    .toBe(1)
  await expect(hint).toHaveCount(0)
})
