import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  apiCheckPath,
  ConnectionWatch,
  connectionMiddleware,
  errorCheckGapMs,
  onlineCheckPath,
  reachesApi,
  reachesSite,
  retryEveryMs,
  type ConnectionEnvironment,
} from './connection'

/** A browser in memory: online or not, with its online/offline and visibility events. */
function fakeBrowser(onLine = true) {
  const windowListeners = new Map<string, Set<() => void>>()
  const documentListeners = new Map<string, Set<() => void>>()
  const add = (map: Map<string, Set<() => void>>) => (type: string, listener: () => void) => {
    map.set(type, (map.get(type) ?? new Set()).add(listener))
  }
  const remove = (map: Map<string, Set<() => void>>) => (type: string, listener: () => void) => {
    map.get(type)?.delete(listener)
  }
  const environment = {
    navigator: { onLine },
    addEventListener: add(windowListeners),
    removeEventListener: remove(windowListeners),
    document: {
      visibilityState: 'visible',
      addEventListener: add(documentListeners),
      removeEventListener: remove(documentListeners),
    },
  } as unknown as ConnectionEnvironment & { document: { visibilityState: string } }
  return {
    environment,
    fire: (type: 'online' | 'offline') =>
      windowListeners.get(type)?.forEach((listener) => listener()),
    show: (state: 'visible' | 'hidden') => {
      environment.document.visibilityState = state
      documentListeners.get('visibilitychange')?.forEach((listener) => listener())
    },
    listenerCount: () =>
      [...windowListeners.values(), ...documentListeners.values()].reduce(
        (sum, set) => sum + set.size,
        0,
      ),
  }
}

const online = {
  offline: false,
  apiDown: false,
  checking: false,
  stillFailing: false,
  reference: null,
}

function watchWith(options: { onLine?: boolean; connected?: boolean; apiUp?: boolean } = {}) {
  const browser = fakeBrowser(options.onLine ?? true)
  let connected = options.connected ?? true
  let apiUp = options.apiUp ?? true
  const check = vi.fn(async () => connected)
  const checkApi = vi.fn(async () => apiUp)
  const reload = vi.fn()
  let time = 0
  const watch = new ConnectionWatch({
    environment: browser.environment,
    check,
    checkApi,
    reload,
    now: () => time,
  })
  return {
    watch,
    browser,
    check,
    checkApi,
    reload,
    connect: (value: boolean) => (connected = value),
    setApiUp: (value: boolean) => (apiUp = value),
    later: (ms: number) => (time += ms),
  }
}

beforeEach(() => {
  vi.useFakeTimers()
})

afterEach(() => {
  vi.useRealTimers()
})

describe('reachesApi', () => {
  it('asks the API’s liveness check, skipping every cache, and needs a good answer', async () => {
    const fetchApi = vi.fn().mockResolvedValue(new Response('{}', { status: 200 }))
    expect(await reachesApi(fetchApi, 'https://api.example')).toBe(true)
    expect(fetchApi).toHaveBeenCalledWith(
      `https://api.example${apiCheckPath}`,
      expect.objectContaining({ cache: 'no-store' }),
    )
    expect(
      await reachesApi(vi.fn().mockResolvedValue(new Response(null, { status: 502 })), 'x'),
    ).toBe(false)
    expect(await reachesApi(vi.fn().mockRejectedValue(new TypeError('Failed to fetch')), 'x')).toBe(
      false,
    )
  })

  it('gives up after the time limit', async () => {
    const fetchApi = vi.fn(
      (_: RequestInfo | URL, init?: RequestInit) =>
        new Promise<Response>((_, reject) =>
          init?.signal?.addEventListener('abort', () =>
            reject(new DOMException('aborted', 'AbortError')),
          ),
        ),
    )
    const result = reachesApi(fetchApi, 'x', 1000)
    await vi.advanceTimersByTimeAsync(1000)
    expect(await result).toBe(false)
  })
})

describe('reachesSite', () => {
  it('asks the app’s own site, skipping every cache, and counts any answer as connected', async () => {
    const fetchSite = vi.fn().mockResolvedValue(new Response(null, { status: 404 }))
    expect(await reachesSite(fetchSite, 5000, () => 42)).toBe(true)
    expect(fetchSite).toHaveBeenCalledWith(
      `${onlineCheckPath}?at=42`,
      expect.objectContaining({ method: 'HEAD', cache: 'no-store' }),
    )
  })

  it('is not connected when the request fails', async () => {
    expect(await reachesSite(vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))).toBe(
      false,
    )
  })

  it('gives up after the time limit', async () => {
    const fetchSite = vi.fn(
      (_: RequestInfo | URL, init?: RequestInit) =>
        new Promise<Response>((_, reject) =>
          init?.signal?.addEventListener('abort', () =>
            reject(new DOMException('aborted', 'AbortError')),
          ),
        ),
    )
    const result = reachesSite(fetchSite as typeof fetch, 1000)
    await vi.advanceTimersByTimeAsync(1000)
    expect(await result).toBe(false)
  })
})

describe('ConnectionWatch', () => {
  it('starts online when the browser is online', () => {
    const { watch } = watchWith()
    expect(watch.getState()).toEqual(online)
  })

  it('shows the offline screen as soon as the browser goes offline, and tells listeners', () => {
    const { watch, browser } = watchWith()
    const listener = vi.fn()
    watch.subscribe(listener)
    watch.start()
    browser.fire('offline')
    expect(watch.getState().offline).toBe(true)
    expect(watch.getState().stillFailing).toBe(false)
    expect(listener).toHaveBeenCalled()
  })

  it('reloads the app when the browser says it’s back and the site answers', async () => {
    const { watch, browser, reload } = watchWith({ onLine: false, connected: true })
    watch.start()
    expect(watch.getState().offline).toBe(true)
    browser.fire('online')
    await vi.runOnlyPendingTimersAsync()
    expect(reload).toHaveBeenCalledTimes(1)
  })

  it('stays offline when the browser says online but the site still doesn’t answer', async () => {
    const { watch, browser, reload } = watchWith({ onLine: false, connected: false })
    watch.start()
    browser.fire('online')
    await Promise.resolve()
    await Promise.resolve()
    expect(watch.getState().offline).toBe(true)
    expect(reload).not.toHaveBeenCalled()
  })

  it('keeps checking every few seconds while offline, and reloads once the connection is back', async () => {
    const { watch, check, reload, connect } = watchWith({ onLine: false, connected: false })
    watch.start()
    await vi.advanceTimersByTimeAsync(retryEveryMs)
    expect(check).toHaveBeenCalledTimes(1)
    expect(reload).not.toHaveBeenCalled()
    connect(true)
    await vi.advanceTimersByTimeAsync(retryEveryMs)
    expect(reload).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(retryEveryMs * 3)
    expect(check).toHaveBeenCalledTimes(2)
  })

  it('checks again when the app comes back to the screen while offline', async () => {
    const { watch, browser, check } = watchWith({ onLine: false, connected: false })
    watch.start()
    browser.show('hidden')
    expect(check).not.toHaveBeenCalled()
    browser.show('visible')
    expect(check).toHaveBeenCalledTimes(1)
  })

  it('doesn’t check on coming back to the screen while online', () => {
    const { watch, browser, check } = watchWith()
    watch.start()
    browser.show('visible')
    expect(check).not.toHaveBeenCalled()
  })

  it('Try again shows "checking", then says when there’s still no connection', async () => {
    const { watch, reload } = watchWith({ onLine: false, connected: false })
    watch.start()
    const trying = watch.tryAgain()
    expect(watch.getState().checking).toBe(true)
    await trying
    expect(watch.getState()).toEqual({ ...online, offline: true, stillFailing: true })
    expect(reload).not.toHaveBeenCalled()
  })

  it('Try again reloads the app when the connection is back', async () => {
    const { watch, reload } = watchWith({ onLine: false, connected: true })
    watch.start()
    await watch.tryAgain()
    expect(reload).toHaveBeenCalledTimes(1)
  })

  it('runs one check at a time', async () => {
    const { watch, check } = watchWith({ onLine: false, connected: false })
    await Promise.all([watch.tryAgain(), watch.tryAgain()])
    expect(check).toHaveBeenCalledTimes(1)
  })

  it('goes offline when an API request fails and the site doesn’t answer either', async () => {
    const { watch } = watchWith({ connected: false })
    watch.reportNetworkError()
    await vi.runOnlyPendingTimersAsync()
    expect(watch.getState().offline).toBe(true)
  })

  it('stays as it is when a request fails but the site and the API both answer (a one-off)', async () => {
    const { watch, check, checkApi } = watchWith({ connected: true })
    watch.reportNetworkError()
    await vi.runOnlyPendingTimersAsync()
    expect(check).toHaveBeenCalledTimes(1)
    expect(checkApi).toHaveBeenCalledTimes(1)
    expect(watch.getState()).toEqual(online)
  })

  it('shows the API screen when the site answers but the API doesn’t: that’s not offline', async () => {
    const { watch, reload } = watchWith({ apiUp: false })
    watch.reportNetworkError('7f3a2c91')
    await vi.runOnlyPendingTimersAsync()
    expect(watch.getState()).toEqual({ ...online, apiDown: true, reference: '7f3a2c91' })
    expect(reload).not.toHaveBeenCalled()
  })

  it('keeps checking the API while it is down, and reloads once it answers', async () => {
    const { watch, checkApi, reload, setApiUp } = watchWith({ apiUp: false })
    watch.reportNetworkError()
    await vi.advanceTimersByTimeAsync(0)
    expect(checkApi).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(retryEveryMs)
    expect(checkApi).toHaveBeenCalledTimes(2)
    expect(reload).not.toHaveBeenCalled()
    setApiUp(true)
    await vi.advanceTimersByTimeAsync(retryEveryMs)
    expect(reload).toHaveBeenCalledTimes(1)
  })

  it('Try again on the API screen says when the API still doesn’t answer, keeping the reference', async () => {
    const { watch } = watchWith({ apiUp: false })
    watch.reportNetworkError('7f3a2c91')
    await vi.runOnlyPendingTimersAsync()
    await watch.tryAgain()
    expect(watch.getState()).toEqual({
      ...online,
      apiDown: true,
      stillFailing: true,
      reference: '7f3a2c91',
    })
  })

  it('switches from the API screen to offline when the connection goes too', async () => {
    const { watch, connect } = watchWith({ apiUp: false })
    watch.reportNetworkError()
    await vi.runOnlyPendingTimersAsync()
    connect(false)
    await watch.tryAgain()
    expect(watch.getState()).toEqual({ ...online, offline: true, stillFailing: true })
  })

  it('checks again on coming back to the screen while the API is down', async () => {
    const { watch, browser, checkApi } = watchWith({ apiUp: false })
    watch.start()
    watch.reportNetworkError()
    await vi.runOnlyPendingTimersAsync()
    const calls = checkApi.mock.calls.length
    browser.show('visible')
    await vi.advanceTimersByTimeAsync(0)
    expect(checkApi.mock.calls.length).toBeGreaterThan(calls)
  })

  it('can pretend the API is down (QA and local copies)', () => {
    const { watch } = watchWith()
    watch.pretendApiDown()
    expect(watch.getState()).toEqual({ ...online, apiDown: true })
  })

  it('checks at most once every few seconds when API requests keep failing', async () => {
    const { watch, check, later } = watchWith({ connected: true })
    watch.reportNetworkError()
    await vi.runOnlyPendingTimersAsync()
    watch.reportNetworkError()
    expect(check).toHaveBeenCalledTimes(1)
    later(errorCheckGapMs)
    watch.reportNetworkError()
    expect(check).toHaveBeenCalledTimes(2)
  })

  it('ignores API failures while already offline', () => {
    const { watch, check } = watchWith({ onLine: false })
    watch.reportNetworkError()
    expect(check).not.toHaveBeenCalled()
  })

  it('stops listening and checking when stopped', async () => {
    const { watch, browser, check } = watchWith({ onLine: false, connected: false })
    const stop = watch.start()
    stop()
    expect(browser.listenerCount()).toBe(0)
    await vi.advanceTimersByTimeAsync(retryEveryMs * 2)
    expect(check).not.toHaveBeenCalled()
  })

  it('stops telling a listener that unsubscribed', () => {
    const { watch, browser } = watchWith()
    const listener = vi.fn()
    const unsubscribe = watch.subscribe(listener)
    unsubscribe()
    watch.start()
    browser.fire('offline')
    expect(listener).not.toHaveBeenCalled()
  })
})

describe('connectionMiddleware', () => {
  const run = (error: unknown) => {
    const watch = { reportNetworkError: vi.fn() }
    const middleware = connectionMiddleware(watch)
    const result = middleware.onError!({ error } as never)
    return { watch, result }
  }

  it('reports requests that got no answer at all', () => {
    const { watch, result } = run(new TypeError('Failed to fetch'))
    expect(watch.reportNetworkError).toHaveBeenCalled()
    expect(result).toBeUndefined()
  })

  it('ignores other errors (a cancelled request)', () => {
    const { watch } = run(new DOMException('aborted', 'AbortError'))
    expect(watch.reportNetworkError).not.toHaveBeenCalled()
  })

  it('reports the gateway answering instead of the API, not the API’s own answers', () => {
    const answer = (response: Response) => {
      const watch = { reportNetworkError: vi.fn() }
      const result = connectionMiddleware(watch).onResponse!({ response } as never)
      expect(result).toBeUndefined()
      return watch.reportNetworkError.mock.calls.length
    }
    const problem = { 'Content-Type': 'application/problem+json' }
    expect(answer(new Response(null, { status: 502 }))).toBe(1)
    expect(answer(new Response(null, { status: 504 }))).toBe(1)
    expect(answer(new Response('down', { status: 503 }))).toBe(1)
    expect(answer(new Response('{}', { status: 503, headers: problem }))).toBe(0)
    expect(answer(new Response('{}', { status: 500, headers: problem }))).toBe(0)
    expect(answer(new Response('{}', { status: 200 }))).toBe(0)
  })
})
