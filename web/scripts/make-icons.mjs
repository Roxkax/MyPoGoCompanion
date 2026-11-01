// Draws the installed app's PNG icons (web/public/icons/) from the app's mark, one set per copy of the app:
// production teal, QA orange, local grey (the same colours as public/icon*.svg). Run once after changing the mark:
//   node web/scripts/make-icons.mjs
// Needs Playwright's Chromium (tests/ui has it: `npm --prefix tests/ui ci`); CHROMIUM_PATH points at another Chromium.
import { createRequire } from 'node:module'
import { mkdirSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const out = resolve(here, '../public/icons')
const require = createRequire(resolve(here, '../../tests/ui/package.json'))
let chromium
try {
  ;({ chromium } = require('@playwright/test'))
} catch {
  ;({ chromium } = require('playwright'))
}

const colours = { production: '#0B7A72', qa: '#A35A00', local: '#5B6E6E' }

/** The mark (public/icon.svg) on a 32×32 grid: a ring with a dot. `full` fills the square edge to edge. */
function mark(colour, { full, scale = 1 }) {
  const ring = `<g transform="translate(16 16) scale(${scale}) translate(-16 -16)"><circle cx="16" cy="16" r="8" fill="none" stroke="#fff" stroke-width="3"/><circle cx="16" cy="16" r="2.5" fill="#fff"/></g>`
  const back = full
    ? `<rect width="32" height="32" fill="${colour}"/>`
    : `<rect width="32" height="32" rx="8" fill="${colour}"/>`
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">${back}${ring}</svg>`
}

// any: the rounded square, as on the tab. maskable: edge to edge, the ring inside the middle 80% (Android crops
// it to its own shape). apple-touch: edge to edge, no transparency (iOS rounds the corners itself).
const sets = [
  { name: 'icon-192.png', size: 192, full: false },
  { name: 'icon-512.png', size: 512, full: false },
  { name: 'maskable-512.png', size: 512, full: true, scale: 0.85 },
  { name: 'apple-touch-icon.png', size: 180, full: true },
]

mkdirSync(out, { recursive: true })
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || undefined })
const page = await browser.newPage()
for (const [kind, colour] of Object.entries(colours)) {
  for (const set of sets) {
    await page.setViewportSize({ width: set.size, height: set.size })
    await page.setContent(
      `<html><body style="margin:0;background:transparent">${mark(colour, set).replace('<svg ', `<svg width="${set.size}" height="${set.size}" `)}</body></html>`,
    )
    const file = resolve(out, kind === 'production' ? set.name : `${kind}-${set.name}`)
    await page.locator('svg').screenshot({ path: file, omitBackground: true })
    console.log(file)
  }
}
await browser.close()
