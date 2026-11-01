import { describe, expect, it, vi } from 'vitest'
import { MemoryStorage } from '../testing/auth'
import { InstallHintStore, hintKey, storageOrNull, visitKey, visitsKey } from './installHint'

/** Storage that refuses everything (some private browsing modes). */
const blocked = {
  getItem: () => {
    throw new Error('blocked')
  },
  setItem: () => {
    throw new Error('blocked')
  },
}

describe('InstallHintStore', () => {
  it('counts one visit per browser session', () => {
    const local = new MemoryStorage()
    const first = new InstallHintStore(local, new MemoryStorage())
    expect(first.countVisit()).toBe(1)
    expect(first.countVisit()).toBe(1)
    expect(new InstallHintStore(local, new MemoryStorage()).countVisit()).toBe(2)
    expect(local.getItem(visitsKey)).toBe('2')
  })

  it('shows from the second visit, only where the app can really be installed', () => {
    const local = new MemoryStorage()
    const session = new MemoryStorage()
    const store = new InstallHintStore(local, session)
    store.countVisit()
    expect(store.shows('prompt')).toBe(false)
    session.removeItem(visitKey)
    store.countVisit()
    expect(store.shows('prompt')).toBe(true)
    expect(store.shows('ios')).toBe(true)
    expect(store.shows('menu')).toBe(false)
    expect(store.shows('installed')).toBe(false)
  })

  it('hides for good once closed, and remembers it', () => {
    const local = new MemoryStorage()
    local.setItem(visitsKey, '5')
    const store = new InstallHintStore(local, new MemoryStorage())
    const listener = vi.fn()
    store.subscribe(listener)
    store.dismiss()
    expect(store.isDismissed()).toBe(true)
    expect(store.shows('prompt')).toBe(false)
    expect(listener).toHaveBeenCalled()
    expect(local.getItem(hintKey)).toBe('dismissed')
    expect(new InstallHintStore(local, new MemoryStorage()).shows('prompt')).toBe(false)
  })

  it('treats a damaged count as no visits', () => {
    const local = new MemoryStorage()
    local.setItem(visitsKey, 'lots')
    expect(new InstallHintStore(local, new MemoryStorage()).visits()).toBe(0)
  })

  it('keeps working when storage is blocked or missing (the hint then never shows)', () => {
    const store = new InstallHintStore(blocked, blocked)
    expect(store.countVisit()).toBe(1)
    expect(store.shows('prompt')).toBe(false)
    expect(() => store.dismiss()).not.toThrow()
    const none = new InstallHintStore(null, null)
    expect(none.countVisit()).toBe(1)
  })

  it('stops telling a listener that unsubscribed', () => {
    const store = new InstallHintStore(new MemoryStorage(), new MemoryStorage())
    const listener = vi.fn()
    store.subscribe(listener)()
    store.dismiss()
    expect(listener).not.toHaveBeenCalled()
  })

  it('uses the browser’s storage by default', () => {
    expect(new InstallHintStore().countVisit()).toBeGreaterThanOrEqual(1)
  })
})

describe('storageOrNull', () => {
  it('hands out the storage, or null when the browser refuses', () => {
    const storage = new MemoryStorage()
    expect(storageOrNull(() => storage)).toBe(storage)
    expect(
      storageOrNull(() => {
        throw new DOMException('denied', 'SecurityError')
      }),
    ).toBeNull()
  })
})
