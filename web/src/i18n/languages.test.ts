import { describe, expect, it } from 'vitest'
import { MemoryStorage } from '../testing/auth'
import { browserLanguage, isLanguage, languageKey, readLanguage, writeLanguage } from './languages'

function brokenStorage(): Storage {
  const fail = () => {
    throw new Error('blocked')
  }
  return { getItem: fail, setItem: fail, removeItem: fail } as unknown as Storage
}

describe('the browser’s language on a first visit', () => {
  it.each([
    [['pt-PT'], 'pt-PT'],
    [['pt-BR', 'en'], 'pt-PT'],
    [['PT'], 'pt-PT'],
    [['en-US'], 'en'],
    [['en-GB', 'pt-PT'], 'en'],
    [['fr-FR', 'pt'], 'pt-PT'],
    [['fr-FR', 'de'], 'en'],
    [[], 'en'],
  ] as const)('%j gives %s', (preferred, expected) => {
    expect(browserLanguage(preferred)).toBe(expected)
  })

  it('reads the browser itself when not told (jsdom says en-US)', () => {
    expect(browserLanguage()).toBe('en')
  })
})

describe('the language saved on this device', () => {
  it('wins over the browser’s', () => {
    const storage = new MemoryStorage()
    writeLanguage('pt-PT', storage)

    expect(storage.getItem(languageKey)).toBe('pt-PT')
    expect(readLanguage(storage, ['en-GB'])).toBe('pt-PT')
  })

  it('nothing (or anything unknown) saved: the browser’s', () => {
    const storage = new MemoryStorage()
    expect(readLanguage(storage, ['pt-BR'])).toBe('pt-PT')

    storage.setItem(languageKey, 'klingon')
    expect(readLanguage(storage, ['en'])).toBe('en')
  })

  it('works without storage (private mode, blocked site data)', () => {
    expect(readLanguage(brokenStorage(), ['pt-PT'])).toBe('pt-PT')
    expect(() => writeLanguage('pt-PT', brokenStorage())).not.toThrow()
    expect(readLanguage(undefined, ['en'])).toBe('en')
  })

  it('knows only the two app languages', () => {
    expect(isLanguage('en')).toBe(true)
    expect(isLanguage('pt-PT')).toBe(true)
    expect(isLanguage('pt-BR')).toBe(false)
    expect(isLanguage(null)).toBe(false)
  })
})
