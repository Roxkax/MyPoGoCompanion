import { describe, expect, it, vi } from 'vitest'
import { MemoryStorage } from '../testing/auth'
import { LanguageStore } from './language'
import { languageKey } from './languages'

describe('LanguageStore', () => {
  it('starts from the browser’s language when nothing is saved, without saving it', () => {
    const storage = new MemoryStorage()
    const store = new LanguageStore(storage, ['pt-PT'])

    expect(store.getLanguage()).toBe('pt-PT')
    expect(store.i18n.t('nav.today')).toBe('Hoje')
    expect(storage.getItem(languageKey)).toBeNull()
  })

  it('changes the language, saves it, marks the page and tells listeners', () => {
    const storage = new MemoryStorage()
    const doc = document.implementation.createHTMLDocument('page')
    const store = new LanguageStore(storage, ['en'], doc)
    const listener = vi.fn()
    store.subscribe(listener)
    store.apply()
    expect(doc.documentElement.lang).toBe('en')

    store.setLanguage('pt-PT')

    expect(store.getLanguage()).toBe('pt-PT')
    expect(storage.getItem(languageKey)).toBe('pt-PT')
    expect(store.i18n.t('nav.settings')).toBe('Definições')
    expect(doc.documentElement.lang).toBe('pt-PT')
    expect(listener).toHaveBeenCalledOnce()

    store.setLanguage('pt-PT')
    expect(listener).toHaveBeenCalledOnce()
  })

  it('follows a change made in another tab, and ignores other keys', () => {
    const storage = new MemoryStorage()
    const store = new LanguageStore(storage, ['en'])
    const listener = vi.fn()
    store.subscribe(listener)

    storage.setItem(languageKey, 'pt-PT')
    store.handleStorageEvent({ key: 'mpgc.theme' })
    expect(store.getLanguage()).toBe('en')

    store.handleStorageEvent({ key: languageKey })
    expect(store.getLanguage()).toBe('pt-PT')

    storage.clear()
    store.handleStorageEvent({ key: null })
    expect(store.getLanguage()).toBe('en')
    expect(listener).toHaveBeenCalledTimes(2)
  })

  it('plurals and placeholders in each language', () => {
    const store = new LanguageStore(new MemoryStorage(), ['en'])
    const { t } = store.i18n

    expect(t('design.daysLeft', { count: 1 })).toBe('1 day left')
    expect(t('design.daysLeft', { count: 3 })).toBe('3 days left')
    expect(t('profile.accountOf', { name: '<Ash>' })).toBe('Account: <Ash>')

    store.setLanguage('pt-PT')
    expect(t('design.daysLeft', { count: 1 })).toBe('Falta 1 dia')
    expect(t('design.daysLeft', { count: 3 })).toBe('Faltam 3 dias')
  })
})
