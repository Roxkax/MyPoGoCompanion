import { expect, test, type Page } from '@playwright/test'

// Pokémon pictures (E02-F05, docs/SPRITES.md): the deploys build PokeMiners' pictures into the web app's /sprites/
// folder; the /design gallery shows real ones, a shiny picture that falls back to the normal one, and the
// placeholder. A local copy has pictures only after `.github/scripts/build_sprites.sh web/public/sprites`.

const examples = [
  { label: 'Charizard', src: '/sprites/pm6.webp' },
  { label: 'Shiny Charizard', src: '/sprites/pm6.s.webp' },
  { label: 'Mega Charizard X', src: '/sprites/pm6.fMEGA_X.webp' },
  { label: 'G-Max Charizard', src: '/sprites/pm6.fGIGANTAMAX.webp' },
  { label: 'Alolan Vulpix', src: '/sprites/pm37.fALOLA.webp' },
  // Urshifu (Rapid Strike) has no shiny picture: its shiny box shows the normal one.
  { label: 'Shiny Urshifu (Rapid Strike)', src: '/sprites/pm892.fRAPID_STRIKE.webp' },
]

const gallery = (page: Page) => page.getByTestId('design-pictures')

async function hasPictures(page: Page): Promise<boolean> {
  const response = await page.request.get('/sprites/set.json')
  return response.ok() && (response.headers()['content-type'] ?? '').includes('json')
}

test('the gallery shows real pictures, a shiny fallback and the placeholder', async ({ page }) => {
  test.skip(!(await hasPictures(page)), 'This copy was built without the pictures.')

  await page.goto('/design')
  const items = gallery(page).getByRole('listitem')
  await expect(items).toHaveCount(examples.length + 1)
  for (const [index, example] of examples.entries()) {
    const item = items.nth(index)
    await expect(item).toContainText(example.label)
    const picture = item.locator('img')
    await picture.scrollIntoViewIfNeeded()
    await expect(picture).toHaveAttribute('src', example.src)
    // Loaded and drawn: a real picture, cropped to a square.
    await expect
      .poll(() => picture.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth))
      .toBeGreaterThan(0)
    const [width, height] = await picture.evaluate((img: HTMLImageElement) => [
      img.naturalWidth,
      img.naturalHeight,
    ])
    expect(width).toBe(height)
  }
  await expect(items.last()).toContainText('No picture')
  await expect(items.last().locator('.ui-sprite-empty')).toBeVisible()
})

test('without pictures every box shows its placeholder, the same size', async ({ page }) => {
  await page.route('**/sprites/*.webp', (route) => route.fulfill({ status: 404, body: '' }))

  await page.goto('/design')
  const items = gallery(page).getByRole('listitem')
  await expect(items).toHaveCount(examples.length + 1)
  await expect(gallery(page).locator('img')).toHaveCount(0)
  await expect(gallery(page).locator('.ui-sprite-empty')).toHaveCount(examples.length + 1)
  const boxes = await gallery(page)
    .locator('.ui-sprite')
    .evaluateAll((spans) => spans.map((span) => span.getBoundingClientRect().width))
  expect(new Set(boxes)).toEqual(new Set([72]))
})

test('the deployed copy serves pictures as WebP kept for a week, and a missing one as not found', async ({
  page,
}) => {
  // Static Web Apps' settings (web/public/staticwebapp.config.json); the local dev server has its own.
  test.skip(!process.env.BASE_URL, 'Only the deployed copy uses the hosting settings.')
  test.skip(!(await hasPictures(page)), 'This copy was built without the pictures.')

  const picture = await page.request.get('/sprites/pm6.webp')
  expect(picture.status()).toBe(200)
  expect(picture.headers()['content-type']).toContain('image/webp')
  expect(picture.headers()['cache-control']).toContain('max-age=604800')

  const missing = await page.request.get('/sprites/pm99999.webp')
  expect(missing.status()).toBe(404)
})
