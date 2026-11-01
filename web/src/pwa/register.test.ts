import { describe, expect, it, vi } from 'vitest'
import { registerServiceWorker } from './register'

function fakeWindow(withServiceWorker: boolean, register = vi.fn().mockResolvedValue({})) {
  const listeners = new Map<string, () => void>()
  const win = {
    addEventListener: (type: string, listener: () => void) => listeners.set(type, listener),
    navigator: withServiceWorker ? { serviceWorker: { register } } : {},
  } as unknown as Pick<Window, 'addEventListener' | 'navigator'>
  return { win, register, load: () => listeners.get('load')?.() }
}

describe('registerServiceWorker', () => {
  it('registers /sw.js for the whole app once the page has loaded', () => {
    const { win, register, load } = fakeWindow(true)
    registerServiceWorker(true, win)
    expect(register).not.toHaveBeenCalled()
    load()
    expect(register).toHaveBeenCalledWith('/sw.js', { scope: '/' })
  })

  it('does nothing on the dev server', () => {
    const { win, register, load } = fakeWindow(true)
    registerServiceWorker(false, win)
    load()
    expect(register).not.toHaveBeenCalled()
  })

  it('does nothing in a browser without service workers', () => {
    const { win, load } = fakeWindow(false)
    expect(() => {
      registerServiceWorker(true, win)
      load()
    }).not.toThrow()
  })

  it('keeps going when the browser refuses (e.g. private browsing)', async () => {
    const register = vi.fn().mockRejectedValue(new Error('refused'))
    const { win, load } = fakeWindow(true, register)
    registerServiceWorker(true, win)
    load()
    await expect(register.mock.results[0]!.value).rejects.toThrow('refused')
  })
})
