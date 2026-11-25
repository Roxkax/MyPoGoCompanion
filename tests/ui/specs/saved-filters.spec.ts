import { expect, test, type Page } from '@playwright/test'

type SavedFilter = {
  id: string
  name: string
  searchString: string
  createdAt: string
  updatedAt: string
}

async function signInAndStandInForStorage(page: Page) {
  await page.addInitScript(() => localStorage.setItem('mpgc.refreshToken', 'a'.repeat(43)))
  await page.route('**/auth/refresh', (route) =>
    route.fulfill({
      json: {
        accessToken: 'saved-filters-ui-test',
        accessTokenExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
        refreshToken: 'b'.repeat(43),
        user: {
          id: '0190e0e0-0000-7000-8000-000000000001',
          email: 'trainer@example.test',
          name: 'Test Trainer',
          pictureUrl: null,
          role: 'User',
          language: 'en',
          theme: 'Device',
        },
      },
    }),
  )
  const saved = new Map<string, SavedFilter>()
  await page.route('**/saved-filters**', async (route) => {
    const request = route.request()
    expect(request.headers().authorization).toBe('Bearer saved-filters-ui-test')
    const [, , id, action] = new URL(request.url()).pathname.split('/')
    const original = id ? saved.get(id) : undefined
    if (id && !original) return route.fulfill({ status: 404 })

    if (request.method() === 'GET') {
      return route.fulfill({ json: original ?? [...saved.values()].reverse() })
    }
    if (request.method() === 'DELETE' && id) {
      saved.delete(id)
      return route.fulfill({ status: 204 })
    }

    const { name, searchString } = request.postDataJSON() as { name: string; searchString: string }
    const now = new Date().toISOString()
    if (original && action === 'name') {
      saved.set(original.id, { ...original, name, updatedAt: now })
      return route.fulfill({ status: 204 })
    }

    const filter = { id: crypto.randomUUID(), name, searchString: original?.searchString ?? searchString, createdAt: now, updatedAt: now }
    saved.set(filter.id, filter)
    return route.fulfill({ status: 201, json: filter })
  })
}

test('saves, copies, renames, duplicates, deletes and reopens a filter', async ({ page, context }) => {
  test.setTimeout(60_000)
  await context.grantPermissions(['clipboard-read', 'clipboard-write'])
  await signInAndStandInForStorage(page)
  await page.goto('/filters/builder')

  const searchString = '+Gible&!shiny'
  await page.getByRole('textbox', { name: /^Search string/ }).fill(searchString)
  await page.getByRole('button', { name: 'Save filter' }).click()
  const save = page.getByRole('dialog', { name: 'Save this filter' })
  await expect(save.getByRole('button', { name: 'Save', exact: true })).toBeDisabled()
  await save.getByRole('textbox', { name: 'Name' }).fill('Community Day')
  await save.getByRole('button', { name: 'Save', exact: true }).click()
  await page.getByRole('link', { name: 'View saved filters' }).click()
  await expect(page.getByRole('link', { name: 'Community Day' })).toBeVisible()

  await page.getByRole('button', { name: 'Copy', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Copied', exact: true })).toBeVisible()
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(searchString)

  await page.getByRole('button', { name: 'Actions for Community Day' }).click()
  await page.getByRole('button', { name: 'Rename', exact: true }).click()
  const rename = page.getByRole('dialog', { name: 'Rename filter' })
  await rename.getByRole('textbox', { name: 'Name' }).fill('Gible day')
  await rename.getByRole('button', { name: 'Rename', exact: true }).click()
  await page.getByRole('button', { name: 'Actions for Gible day' }).click()
  await page.getByRole('button', { name: 'Duplicate', exact: true }).click()
  const duplicate = page.getByRole('dialog', { name: 'Duplicate filter' })
  await expect(duplicate.getByRole('textbox', { name: 'Name' })).toHaveValue('Copy of Gible day')
  await duplicate.getByRole('button', { name: 'Duplicate', exact: true }).click()
  await expect(page.getByRole('link', { name: 'Copy of Gible day' })).toBeVisible()

  await page.getByRole('button', { name: 'Actions for Gible day', exact: true }).click()
  await page.getByRole('button', { name: 'Delete', exact: true }).click()
  const remove = page.getByRole('dialog', { name: 'Delete filter' })
  await remove.getByRole('button', { name: 'Cancel' }).click()
  await expect(page.getByRole('link', { name: 'Gible day', exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Actions for Gible day', exact: true }).click()
  await page.getByRole('button', { name: 'Delete', exact: true }).click()
  await remove.getByRole('button', { name: 'Delete', exact: true }).click()
  await expect(page.getByRole('link', { name: 'Gible day', exact: true })).toHaveCount(0)

  await page.getByRole('link', { name: 'Copy of Gible day' }).click()
  await expect(page.getByRole('textbox', { name: /^Search string/ })).toHaveValue(searchString)
  await page.reload()
  await expect(page.getByRole('textbox', { name: /^Search string/ })).toHaveValue(searchString)
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(page.viewportSize()!.width)
})

test('shows the sign-in prompt on Saved and lets signed-out users build searches', async ({ page }) => {
  await page.goto('/filters/saved')
  await expect(page.getByRole('region', { name: 'Keep your filters' })).toBeVisible()
  await page.goto('/filters/builder')
  const expression = page.getByRole('textbox', { name: /^Search string/ })
  await expression.fill('fire')
  await page.getByRole('button', { name: 'Save filter' }).click()
  const dialog = page.getByRole('dialog', { name: 'Sign in to make it yours' })
  await expect(dialog).toBeVisible()
  await dialog.getByRole('button', { name: 'Not now, keep browsing' }).click()
  await expect(expression).toHaveValue('fire')
})
