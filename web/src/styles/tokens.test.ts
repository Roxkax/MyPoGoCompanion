/// <reference types="node" />
import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

// Read from disk: Vitest doesn't load CSS into tests.
const read = (file: string) => readFileSync(fileURLToPath(new URL(file, import.meta.url)), 'utf8')
const indexCss = read('../index.css')
const shellCss = read('./shell.css')
const tokensCss = read('./tokens.css')
const uiCss = read('./ui.css')

const colour = /#[0-9a-f]{3,8}\b|rgba?\(/i

/** The custom properties declared in the first `:root { … }` block of tokens.css. */
function rootTokens(): [string, string][] {
  const block = tokensCss.slice(tokensCss.indexOf(':root {'), tokensCss.indexOf('\n}\n'))
  return [...block.matchAll(/(--[\w-]+):\s*([^;]+);/g)].map((m) => [m[1]!, m[2]!.trim()])
}

describe('design tokens', () => {
  it('every colour has a dark version (light-dark), except the type colours and the avatar', () => {
    const fixed = rootTokens().filter(
      ([name, value]) =>
        colour.test(value) &&
        !value.includes('light-dark(') &&
        !/^--t-/.test(name) &&
        name !== '--avatar',
    )

    expect(fixed).toEqual([])
  })

  it('has the colours of the approved design (base.css)', () => {
    const tokens = Object.fromEntries(rootTokens())

    expect(tokens['--accent']).toBe('light-dark(#0b7a72, #3cc6b6)')
    expect(tokens['--bg']).toBe('light-dark(#f2f6f5, #0d1514)')
    expect(tokens['--t-dragon']).toBe('#4552c4')
  })

  it('a colour for each of the 18 types, used by a badge style', () => {
    const types = rootTokens().filter(([name]) => /^--t-[a-z]+$/.test(name) && name !== '--t-ink')

    expect(types).toHaveLength(18)
    for (const [name] of types) {
      const type = name.slice('--t-'.length)
      expect(uiCss).toMatch(
        new RegExp(`\\[data-type='${type}'\\] \\{\\s*--type: var\\(${name}\\);`),
      )
    }
  })

  it('Light and Dark set the colour scheme', () => {
    expect(tokensCss).toMatch(/:root\[data-theme='light'\] \{\s*color-scheme: light;/)
    expect(tokensCss).toMatch(/:root\[data-theme='dark'\] \{\s*color-scheme: dark;/)
  })

  it.each([
    ['shell.css', shellCss],
    ['ui.css', uiCss],
  ])('%s takes its colours from the tokens only', (_, css) => {
    const lines = css.split('\n').filter((line) => colour.test(line))

    expect(lines).toEqual([])
  })

  it("index.css's only fixed colours are the avatar's white letter and Google's sign-in button", () => {
    const lines = indexCss
      .split('\n')
      .filter((line) => colour.test(line))
      .map((line) => line.trim())

    expect(lines).toEqual([
      'color: #fff;',
      'border: 1px solid #dadce0;',
      'background: #fff;',
      'color: #1f1f1f;',
    ])
  })
})
