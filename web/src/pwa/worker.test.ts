import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  appPage,
  cacheName,
  precachePaths,
  startServiceWorker,
  type FetchLikeEvent,
  type PrecacheEntry,
  type WaitUntilEvent,
  type WorkerScope,
} from './worker'

const entries: PrecacheEntry[] = [
  { url: 'index.html', revision: 'r1' },
  { url: 'assets/index-abc.js', revision: null },
  { url: 'icon.svg', revision: 'r2' },
]

const origin = 'https://app.example'

/** A service worker's world in memory: caches, the network and the events the browser sends. */
function fakeWorker(
  network: (request: Request) => Promise<Response> = () => Promise.resolve(new Response('online')),
) {
  const stores = new Map<string, Map<string, Response>>()
  const listeners = new Map<string, (event: never) => void>()
  const added: Request[] = []
  const caches = {
    open: vi.fn(async (name: string) => {
      const store = stores.get(name) ?? new Map<string, Response>()
      stores.set(name, store)
      return {
        addAll: async (requests: Request[]) => {
          for (const request of requests) {
            added.push(request)
            const path = new URL(request.url).pathname
            store.set(path, new Response(`saved ${path}`))
          }
        },
      }
    }),
    keys: async () => [...stores.keys()],
    delete: vi.fn(async (name: string) => stores.delete(name)),
    match: async (path: string, options?: { cacheName?: string }) =>
      stores
        .get(options?.cacheName ?? '')
        ?.get(path)
        ?.clone(),
  }
  const scope: WorkerScope = {
    addEventListener: (type: string, listener: (event: never) => void) =>
      listeners.set(type, listener),
    skipWaiting: vi.fn(() => Promise.resolve()),
    clients: { claim: vi.fn(() => Promise.resolve()) },
    location: { origin },
    caches: caches as unknown as CacheStorage,
    fetch: vi.fn(network),
  } as WorkerScope

  async function lifecycle(type: 'install' | 'activate') {
    let work: Promise<unknown> = Promise.resolve()
    const event: WaitUntilEvent = { waitUntil: (promise) => (work = promise) }
    ;(listeners.get(type) as (event: WaitUntilEvent) => void)(event)
    await work
  }

  /** Sends a request through the worker: its answer, or `undefined` when it leaves the request to the browser. */
  async function request(path: string, init: { method?: string; mode?: string } = {}) {
    let answer: Promise<Response> | undefined
    const event: FetchLikeEvent = {
      request: {
        url: new URL(path, origin).href,
        method: init.method ?? 'GET',
        mode: init.mode ?? 'cors',
      } as Request,
      respondWith: (response) => (answer = response),
    }
    ;(listeners.get('fetch') as (event: FetchLikeEvent) => void)(event)
    return answer && (await answer)
  }

  return { scope, stores, added, caches, lifecycle, request }
}

afterEach(() => {
  vi.useRealTimers()
})

describe('the build file list', () => {
  it('turns the build list into the addresses the browser asks for', () => {
    expect(precachePaths(entries)).toEqual(['/index.html', '/assets/index-abc.js', '/icon.svg'])
  })

  it('names the saved copy after the build, so every build gets its own', () => {
    expect(cacheName(entries)).toMatch(/^app-[0-9a-z]+$/)
    expect(cacheName(entries)).toBe(cacheName([...entries]))
    expect(cacheName([{ ...entries[0]!, revision: 'r9' }, ...entries.slice(1)])).not.toBe(
      cacheName(entries),
    )
  })
})

describe('installing and taking over', () => {
  it('saves every file of the build straight from the server, then takes over at once', async () => {
    const worker = fakeWorker()
    startServiceWorker(worker.scope, entries)
    await worker.lifecycle('install')

    expect(worker.caches.open).toHaveBeenCalledWith(cacheName(entries))
    expect(worker.added.map((request) => new URL(request.url).pathname)).toEqual(
      precachePaths(entries),
    )
    expect(worker.added.every((request) => request.cache === 'reload')).toBe(true)
    expect(worker.scope.skipWaiting).toHaveBeenCalled()
  })

  it('removes the copies saved by older builds, keeping anything else', async () => {
    const worker = fakeWorker()
    worker.stores.set('app-old', new Map())
    worker.stores.set('something-else', new Map())
    startServiceWorker(worker.scope, entries)
    await worker.lifecycle('install')
    await worker.lifecycle('activate')

    expect([...worker.stores.keys()].sort()).toEqual([cacheName(entries), 'something-else'].sort())
    expect(worker.scope.clients.claim).toHaveBeenCalled()
  })
})

describe('answering requests', () => {
  it('gets pages from the network while online, so the app is always the latest', async () => {
    const worker = fakeWorker()
    startServiceWorker(worker.scope, entries)
    await worker.lifecycle('install')

    const answer = await worker.request('/pokedex', { mode: 'navigate' })
    expect(await answer!.text()).toBe('online')
  })

  it('opens the saved app page at any address when the network fails', async () => {
    const worker = fakeWorker(() => Promise.reject(new TypeError('Failed to fetch')))
    startServiceWorker(worker.scope, entries)
    await worker.lifecycle('install')

    const answer = await worker.request('/battle/raids', { mode: 'navigate' })
    expect(await answer!.text()).toBe(`saved ${appPage}`)
  })

  it('opens the saved page when the network takes too long', async () => {
    vi.useFakeTimers()
    const worker = fakeWorker(() => new Promise<Response>(() => {}))
    startServiceWorker(worker.scope, entries, { timeoutMs: 1000 })
    await worker.lifecycle('install')

    const pending = worker.request('/', { mode: 'navigate' })
    await vi.advanceTimersByTimeAsync(1000)
    expect(await (await pending)!.text()).toBe(`saved ${appPage}`)
  })

  it('fails like the browser would when offline before anything was saved', async () => {
    const worker = fakeWorker(() => Promise.reject(new TypeError('Failed to fetch')))
    startServiceWorker(worker.scope, entries)

    const answer = await worker.request('/', { mode: 'navigate' })
    expect(answer!.type).toBe('error')
  })

  it('serves the build’s own files from the saved copy', async () => {
    const worker = fakeWorker()
    startServiceWorker(worker.scope, entries)
    await worker.lifecycle('install')

    const answer = await worker.request('/assets/index-abc.js')
    expect(await answer!.text()).toBe('saved /assets/index-abc.js')
    expect(worker.scope.fetch).not.toHaveBeenCalled()
  })

  it('falls back to the network for a build file that isn’t saved yet', async () => {
    const worker = fakeWorker()
    startServiceWorker(worker.scope, entries)

    const answer = await worker.request('/icon.svg')
    expect(await answer!.text()).toBe('online')
  })

  it('leaves everything else to the browser: other files, other sites (the API) and changes', async () => {
    const worker = fakeWorker()
    startServiceWorker(worker.scope, entries)
    await worker.lifecycle('install')

    expect(await worker.request('/assets/index-new.js')).toBeUndefined()
    expect(await worker.request('/online-check')).toBeUndefined()
    expect(await worker.request('https://api.example/health')).toBeUndefined()
    expect(await worker.request('https://api.example/me', { mode: 'navigate' })).toBeUndefined()
    expect(await worker.request('/', { method: 'POST', mode: 'navigate' })).toBeUndefined()
  })
})
