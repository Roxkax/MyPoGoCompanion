import { expect, test, type Page } from '@playwright/test'

// Sign-in (E01-F02, screen F1). Google itself can't be driven by a test, so Google's page is never opened (a stand-in
// page answers instead) and the API's sign-in answers are stood in for where a real Google account would be needed.
// Everything else (the screens, the redirect to Google, the return, signing out) is the real app.

const superAdmin = {
  id: '0190e0e0-0000-7000-8000-000000000001',
  email: 'owner@example.test',
  name: 'Test Owner',
  pictureUrl: null,
  role: 'SuperAdmin',
  language: 'en',
  theme: 'Device',
}

function session(refreshToken: string) {
  return {
    accessToken: `access-${refreshToken}`,
    accessTokenExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
    refreshToken,
    user: superAdmin,
  }
}

const token = (letter: string) => letter.repeat(43)

/** Stands in for the API's sign-in answers and records what the app sent. */
async function fakeSignInApi(page: Page) {
  const calls: { path: string; body: unknown }[] = []
  await page.route('**/auth/google/client', (route) =>
    route.fulfill({ json: { clientId: 'ui-test-client' } }),
  )
  for (const [path, answer] of [
    ['/auth/google', session(token('b'))],
    ['/auth/refresh', session(token('c'))],
  ] as const) {
    await page.route(`**${path}`, (route) => {
      calls.push({ path, body: route.request().postDataJSON() })
      return route.fulfill({ json: answer })
    })
  }
  await page.route('**/auth/sign-out', (route) => {
    calls.push({ path: '/auth/sign-out', body: route.request().postDataJSON() })
    return route.fulfill({ status: 204 })
  })
  // Never actually reach Google: a stand-in page answers its address.
  await page.route('https://accounts.google.com/**', (route) =>
    route.fulfill({ contentType: 'text/html', body: '<p>Google (stand-in)</p>' }),
  )
  return calls
}

async function leaveForGoogle(page: Page): Promise<URL> {
  const [request] = await Promise.all([
    page.waitForRequest((r) => r.url().startsWith('https://accounts.google.com/')),
    page.getByRole('dialog').getByRole('button', { name: 'Continue with Google' }).click(),
  ])
  await page.waitForURL(/^https:\/\/accounts\.google\.com\//)
  return new URL(request.url())
}

test('signed out: the profile button opens the sign-in screen, "Not now" closes it', async ({
  page,
}) => {
  await page.goto('/')

  await expect(page.getByRole('region', { name: 'Your collection lives here' })).toBeVisible()
  await page.getByRole('button', { name: 'Sign in' }).click()

  const dialog = page.getByRole('dialog', { name: 'Sign in to make it yours' })
  await expect(dialog).toBeVisible()
  await expect(dialog).toContainText('Everything else works without an account')
  await expect(dialog.getByRole('listitem')).toHaveCount(4)
  await expect(dialog).toContainText('No passwords')

  await dialog.getByRole('button', { name: 'Not now, keep browsing' }).click()
  await expect(dialog).toBeHidden()

  await page.getByRole('button', { name: 'Sign in' }).click()
  await page.keyboard.press('Escape')
  await expect(dialog).toBeHidden()
})

test('Continue with Google sends the person to Google with PKCE and comes back signed in', async ({
  page,
}) => {
  const calls = await fakeSignInApi(page)
  await page.goto('/?from=test')
  const origin = new URL(page.url()).origin
  await page.getByRole('button', { name: 'Sign in' }).click()

  const google = await leaveForGoogle(page)
  expect(google.origin + google.pathname).toBe('https://accounts.google.com/o/oauth2/v2/auth')
  expect(Object.fromEntries(google.searchParams)).toMatchObject({
    client_id: 'ui-test-client',
    redirect_uri: `${origin}/auth/callback`,
    response_type: 'code',
    scope: 'openid email profile',
    code_challenge_method: 'S256',
  })
  expect(google.searchParams.get('code_challenge')).toMatch(/^[A-Za-z0-9_-]{43}$/)

  // Google sends the person back with a code and the same state.
  await page.goto(
    `/auth/callback?code=from-google&state=${encodeURIComponent(google.searchParams.get('state')!)}`,
  )

  await expect(page.getByRole('button', { name: 'Account: Test Owner' })).toBeVisible()
  await expect(page).toHaveURL(/\/\?from=test$/)
  await expect(page.getByRole('region', { name: 'Signed in' })).toContainText('owner@example.test')
  const signIn = calls.find((call) => call.path === '/auth/google')!.body as Record<string, string>
  expect(signIn.code).toBe('from-google')
  expect(signIn.redirectUri).toBe(`${origin}/auth/callback`)
  expect(signIn.codeVerifier).toMatch(/^[A-Za-z0-9\-._~]{64}$/)
  expect(await page.evaluate(() => localStorage.getItem('mpgc.refreshToken'))).toBe(token('b'))
})

test('a callback with the wrong state is refused', async ({ page }) => {
  await fakeSignInApi(page)
  await page.goto('/')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await leaveForGoogle(page)

  await page.goto('/auth/callback?code=from-google&state=forged')

  await expect(page.getByRole('alert')).toContainText('This sign-in link has expired.')
  await page.getByRole('button', { name: 'Back to the app' }).click()
  await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible()
})

test('a returning visitor stays signed in; the account menu signs out', async ({ page }) => {
  const calls = await fakeSignInApi(page)
  await page.addInitScript((stored) => {
    if (!sessionStorage.getItem('seeded')) {
      localStorage.setItem('mpgc.refreshToken', stored)
      sessionStorage.setItem('seeded', '1')
    }
  }, token('a'))
  await page.goto('/')

  const account = page.getByRole('button', { name: 'Account: Test Owner' })
  await expect(account).toBeVisible()
  expect(calls.find((call) => call.path === '/auth/refresh')!.body).toEqual({
    refreshToken: token('a'),
  })

  await account.click()
  const menu = page.getByRole('menu', { name: 'Account' })
  await expect(menu).toContainText('owner@example.test')
  await expect(menu).toContainText('Super admin')
  await menu.getByRole('menuitem', { name: 'Sign out', exact: true }).click()

  await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible()
  await expect.poll(() => calls.some((call) => call.path === '/auth/sign-out')).toBe(true)
  expect(await page.evaluate(() => localStorage.getItem('mpgc.refreshToken'))).toBeNull()

  await page.reload()
  await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible()
})

test('the real API answers the sign-in set-up question', async ({ page, request }) => {
  // On QA this is the deployed API: 200 with a client id once Google is set up, 503 before. Never a crash.
  await page.goto('/')
  const apiUrl = await page.evaluate(() =>
    performance
      .getEntriesByType('resource')
      .map((entry) => entry.name)
      .find((name) => name.endsWith('/health')),
  )
  test.skip(!apiUrl, 'the page did not call the API')
  const response = await request.get(apiUrl!.replace(/\/health$/, '/auth/google/client'))
  expect([200, 503]).toContain(response.status())
})
