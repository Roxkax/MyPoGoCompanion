import { describe, expect, it, vi } from 'vitest'
import {
  InstallWatch,
  isAppleMobile,
  standaloneQuery,
  type BeforeInstallPromptEvent,
  type InstallEnvironment,
} from './install'

const chromeAndroid =
  'Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0 Mobile Safari/537.36'
const iPhone =
  'Mozilla/5.0 (iPhone; CPU iPhone OS 19_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/19.0 Mobile/15E148 Safari/604.1'
const macSafari =
  'Mozilla/5.0 (Macintosh; Intel Mac OS X 15_0) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/19.0 Safari/605.1.15'

function fakeEnvironment(
  options: {
    userAgent?: string
    maxTouchPoints?: number
    standalone?: boolean
    displayStandalone?: boolean
  } = {},
) {
  const listeners = new Map<string, Set<(event: Event) => void>>()
  const environment: InstallEnvironment = {
    addEventListener: (type, listener) =>
      listeners.set(type, (listeners.get(type) ?? new Set()).add(listener)),
    removeEventListener: (type, listener) => listeners.get(type)?.delete(listener),
    matchMedia: (query) => ({
      matches: query === standaloneQuery && options.displayStandalone === true,
    }),
    navigator: {
      userAgent: options.userAgent ?? chromeAndroid,
      maxTouchPoints: options.maxTouchPoints,
      standalone: options.standalone,
    },
  }
  return {
    environment,
    fire: (type: string, event: Event = new Event(type)) =>
      listeners.get(type)?.forEach((listener) => listener(event)),
    count: () => [...listeners.values()].reduce((sum, set) => sum + set.size, 0),
  }
}

/** Chrome's announcement, answering the install window with `outcome`. */
function announcement(outcome: 'accepted' | 'dismissed' = 'accepted') {
  const event = new Event('beforeinstallprompt', { cancelable: true }) as BeforeInstallPromptEvent
  const prompt = vi.fn(() => Promise.resolve())
  Object.assign(event, { prompt, userChoice: Promise.resolve({ outcome }) })
  return { event, prompt }
}

describe('isAppleMobile', () => {
  it('knows iPhones and iPads (which say Macintosh but have a touch screen)', () => {
    expect(isAppleMobile({ userAgent: iPhone })).toBe(true)
    expect(isAppleMobile({ userAgent: macSafari, maxTouchPoints: 5 })).toBe(true)
    expect(isAppleMobile({ userAgent: macSafari, maxTouchPoints: 0 })).toBe(false)
    expect(isAppleMobile({ userAgent: macSafari })).toBe(false)
    expect(isAppleMobile({ userAgent: chromeAndroid })).toBe(false)
  })
})

describe('InstallWatch', () => {
  it('has nothing to offer when opened as the installed app', () => {
    expect(
      new InstallWatch(fakeEnvironment({ displayStandalone: true }).environment).getKind(),
    ).toBe('installed')
    expect(
      new InstallWatch(
        fakeEnvironment({ userAgent: iPhone, standalone: true }).environment,
      ).getKind(),
    ).toBe('installed')
  })

  it('offers the guide on iPhone and the browser menu elsewhere until the browser announces it can install', () => {
    expect(new InstallWatch(fakeEnvironment({ userAgent: iPhone }).environment).getKind()).toBe(
      'ios',
    )
    expect(new InstallWatch(fakeEnvironment().environment).getKind()).toBe('menu')
  })

  it('works without matchMedia', () => {
    const { environment } = fakeEnvironment()
    delete environment.matchMedia
    expect(new InstallWatch(environment).getKind()).toBe('menu')
  })

  it('keeps Chrome’s announcement for its own button instead of Chrome’s banner', () => {
    const browser = fakeEnvironment()
    const watch = new InstallWatch(browser.environment)
    const listener = vi.fn()
    watch.subscribe(listener)
    watch.start()
    const { event } = announcement()
    browser.fire('beforeinstallprompt', event)
    expect(event.defaultPrevented).toBe(true)
    expect(watch.getKind()).toBe('prompt')
    expect(listener).toHaveBeenCalled()
  })

  it('opens the browser’s install window and is installed when accepted', async () => {
    const browser = fakeEnvironment()
    const watch = new InstallWatch(browser.environment)
    watch.start()
    const { event, prompt } = announcement('accepted')
    browser.fire('beforeinstallprompt', event)
    expect(await watch.prompt()).toBe('accepted')
    expect(prompt).toHaveBeenCalledTimes(1)
    expect(watch.getKind()).toBe('installed')
  })

  it('uses an announcement once: refused, it waits for the next one', async () => {
    const browser = fakeEnvironment()
    const watch = new InstallWatch(browser.environment)
    watch.start()
    browser.fire('beforeinstallprompt', announcement('dismissed').event)
    expect(await watch.prompt()).toBe('dismissed')
    expect(watch.getKind()).toBe('menu')
    expect(await watch.prompt()).toBe('unavailable')
  })

  it('knows when the app was installed from the browser’s own button', () => {
    const browser = fakeEnvironment()
    const watch = new InstallWatch(browser.environment)
    watch.start()
    browser.fire('beforeinstallprompt', announcement().event)
    browser.fire('appinstalled')
    expect(watch.getKind()).toBe('installed')
  })

  it('stops listening when stopped, and stops telling a listener that unsubscribed', () => {
    const browser = fakeEnvironment()
    const watch = new InstallWatch(browser.environment)
    const listener = vi.fn()
    watch.subscribe(listener)()
    const stop = watch.start()
    browser.fire('appinstalled')
    expect(listener).not.toHaveBeenCalled()
    stop()
    expect(browser.count()).toBe(0)
  })
})
