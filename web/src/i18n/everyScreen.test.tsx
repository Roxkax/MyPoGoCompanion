import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { readEnvironment } from '../environment'
import { admin, renderApp } from '../testing/app'
import { MemoryStorage } from '../testing/auth'
import { LanguageStore } from './language'
import { en } from './messages/en'
import { ptPT } from './messages/pt-PT'

type Tree = { [key: string]: string | Tree }

function leaves(tree: Tree): string[] {
  return Object.values(tree).flatMap((value) =>
    typeof value === 'string' ? [value] : leaves(value),
  )
}

/** English texts whose Portuguese is different: none of them may show on a Portuguese screen. */
const portuguese = new Set(leaves(ptPT))
const englishOnly = new Set(leaves(en).filter((text) => !portuguese.has(text)))

/** Every text node and accessible label on the page. */
function shownTexts(): string[] {
  const texts: string[] = []
  const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT)
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    const text = node.textContent?.trim()
    if (text) texts.push(text)
  }
  document.querySelectorAll('[aria-label], [alt]').forEach((element) => {
    texts.push(element.getAttribute('aria-label') ?? '', element.getAttribute('alt') ?? '')
  })
  return texts.filter(Boolean)
}

const addresses = [
  '/',
  '/calendar',
  '/calendar/community-day',
  '/eggs',
  '/pokedex',
  '/pokedex/garchomp',
  '/pokedex/moves',
  '/pokedex/moves/earthquake',
  '/pokedex/types',
  '/battle/raids',
  '/battle/raids/kyogre',
  '/battle/tiers/great-league',
  '/battle/tiers/raids/dragon',
  '/battle/teams',
  '/filters',
  '/filters/builder',
  '/filters/saved',
  '/collection',
  '/collection/upload',
  '/collection/imports/1',
  '/collection/imports/1/changes',
  '/settings',
  '/admin',
  '/admin/entries',
  '/design',
  '/nowhere',
]

beforeEach(() => vi.stubGlobal('fetch', () => new Promise(() => {})))
afterEach(() => vi.unstubAllGlobals())

describe('in Português (Portugal), no English is left', () => {
  const portugueseDevice = () => new LanguageStore(new MemoryStorage(), ['pt-PT'])
  const local = readEnvironment({ VITE_APP_ENV: 'local' })

  it.each(addresses.flatMap((path) => [[path, 'phone'] as const, [path, 'desktop'] as const]))(
    '%s (%s)',
    (path, layout) => {
      renderApp(path, {
        layout,
        user: { ...admin, language: 'pt-PT' },
        languages: portugueseDevice(),
        environment: local,
      })

      const leftovers = shownTexts().filter((text) => englishOnly.has(text))
      expect(leftovers).toEqual([])
    },
  )

  it('signed out: the sign-in window, prompts and profile menu', async () => {
    renderApp('/collection', { languages: portugueseDevice(), environment: local })

    await userEvent.click(screen.getByRole('button', { name: 'Perfil' }))
    expect(shownTexts().filter((text) => englishOnly.has(text))).toEqual([])
    await userEvent.click(screen.getByRole('menuitem', { name: 'Iniciar sessão' }))

    expect(screen.getByRole('dialog', { name: 'Inicia sessão para a tornar tua' })).toBeVisible()
    expect(shownTexts().filter((text) => englishOnly.has(text))).toEqual([])
  })

  it('search and Ask', async () => {
    renderApp('/', { languages: portugueseDevice(), user: { ...admin, language: 'pt-PT' } })

    await userEvent.click(screen.getByRole('button', { name: /Pesquisar Pokémon/ }))
    expect(screen.getByRole('dialog', { name: 'Pesquisar' })).toBeVisible()
    expect(shownTexts().filter((text) => englishOnly.has(text))).toEqual([])

    await userEvent.keyboard('{Escape}')
    await userEvent.click(screen.getByRole('button', { name: 'Perguntar' }))
    expect(screen.getByRole('dialog', { name: 'Perguntar' })).toBeVisible()
    expect(shownTexts().filter((text) => englishOnly.has(text))).toEqual([])
  })
})
