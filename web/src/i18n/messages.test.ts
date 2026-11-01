import { describe, expect, it } from 'vitest'
import { en } from './messages/en'
import { ptPT } from './messages/pt-PT'

type Tree = { [key: string]: string | Tree }

function leaves(tree: Tree, prefix = ''): [string, string][] {
  return Object.entries(tree).flatMap(([key, value]) =>
    typeof value === 'string' ? [[prefix + key, value]] : leaves(value, `${prefix}${key}.`),
  )
}

const english = new Map(leaves(en))
const portuguese = new Map(leaves(ptPT))
const placeholders = (text: string) =>
  [...text.matchAll(/\{\{\s*(\w+)\s*\}\}/g)].map((m) => m[1]).sort()

describe('the texts', () => {
  it('Portuguese has every English key and no others', () => {
    expect([...portuguese.keys()].sort()).toEqual([...english.keys()].sort())
  })

  it('no text is empty', () => {
    for (const [key, text] of [...english, ...portuguese]) {
      expect(text.trim(), key).not.toBe('')
    }
  })

  it('each translation keeps the same {{placeholders}}', () => {
    for (const [key, text] of english) {
      expect(placeholders(portuguese.get(key)!), key).toEqual(placeholders(text))
    }
  })

  it('plurals come in pairs (one and other)', () => {
    const plural = [...english.keys()].filter((key) => /_(one|other)$/.test(key))
    for (const key of plural) {
      const base = key.replace(/_(one|other)$/, '')
      expect(english.has(`${base}_one`) && english.has(`${base}_other`), base).toBe(true)
    }
  })
})
