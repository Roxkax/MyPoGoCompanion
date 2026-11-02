import { expect, test, type Page, type Route } from '@playwright/test'

// Errors (E01-F08): a screen that breaks shows "Something went wrong" inside the frame and is reported to the API
// with the reference it shows; when the internet works but the API doesn't answer, "isn't answering" takes the
// screen's place and the app comes back by itself. QA and local copies can break on purpose with ?fail=.

const isDesktop = (name: string) => name.startsWith('desktop')

/** The frame stays around an error: sidebar on a desktop, tab bar on a phone. */
async function expectFrame(page: Page, desktop: boolean) {
  if (desktop) {
    await expect(page.getByRole('complementary')).toBeVisible()
  } else {
    await expect(page.getByRole('navigation', { name: 'Tabs' })).toBeVisible()
  }
}

async function expectNoSidewaysScroll(page: Page) {
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  )
  expect(overflow).toBeLessThanOrEqual(0)
}

/** API calls (not the app's own files) get no answer, the way a stopped API looks to the browser. */
async function stopTheApi(
  page: Page,
  answer: (route: Route) => Promise<void> = (route) => route.abort(),
) {
  const isApi = (url: URL) =>
    /^\/(health|freshness|auth|me|admin|client-errors)(\/|$)/.test(url.pathname)
  await page.route(isApi, answer)
  return () => page.unroute(isApi)
}

test('a screen that breaks: "Something went wrong" inside the frame, reported with its reference', async ({
  page,
}, testInfo) => {
  const reported = page.waitForRequest(
    (request) => request.method() === 'POST' && request.url().endsWith('/client-errors'),
  )
  await page.goto('/pokedex?fail=screen')

  const card = page.locator('main.route-error')
  await expect(page.getByRole('heading', { name: 'Something went wrong' })).toBeVisible()
  await expectFrame(page, isDesktop(testInfo.project.name))

  const request = await reported
  const body = request.postDataJSON() as {
    reference: string
    kind: string
    message: string
    path: string
  }
  expect(body.kind).toBe('Screen')
  expect(body.reference).toMatch(/^[0-9a-f]{8}$/)
  expect(body.message).toContain('?fail=screen')
  expect(body.path).toBe('/pokedex')
  await expect(card.locator('.error-reference')).toHaveText(`Reference ${body.reference}`)
  expect((await request.response())?.status()).toBe(202)
  await expectNoSidewaysScroll(page)

  await page.getByRole('link', { name: 'Go to Today' }).click()
  await expect(page).toHaveURL(/\/$/)
  await expect(page.getByRole('heading', { name: 'Something went wrong' })).toHaveCount(0)
})

test('a script error nobody catches is reported, and nothing changes on screen', async ({
  page,
}) => {
  const reported = page.waitForRequest(
    (request) => request.method() === 'POST' && request.url().endsWith('/client-errors'),
  )
  await page.goto('/settings?fail=script')
  const body = (await reported).postDataJSON() as {
    kind: string
    message: string
  }
  expect(body.kind).toBe('Script')
  expect(body.message).toContain('?fail=script')
  await expect(page.getByRole('heading', { name: 'Something went wrong' })).toHaveCount(0)
})

test('the API stops answering: "isn’t answering" in the frame, and the app comes back by itself', async ({
  page,
}, testInfo) => {
  await page.goto('/pokedex')
  await expect(page.getByRole('main')).toBeVisible()
  const restart = await stopTheApi(page)

  // The next API call fails; the site still answers, the API's liveness check doesn't.
  await page.goto('/')
  const title = page.getByRole('heading', {
    name: 'MyPoGoCompanion isn’t answering right now',
  })
  await expect(title).toBeVisible({ timeout: 15_000 })
  await expectFrame(page, isDesktop(testInfo.project.name))
  await expectNoSidewaysScroll(page)

  await page.getByRole('button', { name: 'Try again' }).click()
  await expect(page.getByText(/Still no answer/)).toBeVisible()

  await restart()
  await page.getByRole('button', { name: 'Try again' }).click()
  await expect(title).toHaveCount(0, { timeout: 15_000 })
  await expect(page.getByRole('main')).toBeVisible()
})

test('Azure’s gateway answering instead of the API counts as not answering', async ({ page }) => {
  await stopTheApi(page, (route) => route.fulfill({ status: 502, body: 'Bad Gateway' }))
  await page.goto('/')
  await expect(
    page.getByRole('heading', {
      name: 'MyPoGoCompanion isn’t answering right now',
    }),
  ).toBeVisible({ timeout: 15_000 })
})

test('"isn’t answering", in Portuguese', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('mpgc.language', 'pt-PT'))
  await page.goto('/?fail=api')
  await expect(
    page.getByRole('heading', {
      name: 'O MyPoGoCompanion não está a responder',
    }),
  ).toBeVisible()
  await expect(page.getByRole('button', { name: 'Tentar de novo' })).toBeVisible()
})

test('the design gallery shows the error card and the ways to break on purpose', async ({
  page,
}) => {
  await page.goto('/design')
  const card = page.locator('.load-error')
  await expect(card).toContainText('This couldn’t be loaded')
  await expect(card.locator('.error-reference')).toHaveText('Reference 4bf92f35')
  await expect(page.getByRole('link', { name: '?fail=screen' })).toBeVisible()
})
