// The service worker's work (src/sw.ts runs it): keeps a copy of the app itself (its page, scripts, styles, fonts and
// icons) so the app still opens without a connection and can show the "You're offline" screen (H3).
//
// - Pages come from the network first, so online the app is always the latest deploy (no "update available"
//   message, decision 2026-11-01); the saved page is used only when the network fails or takes too long.
// - Scripts, styles, fonts and icons of this version come from the saved copy (their names change with every build).
// - Nothing else is touched: API answers are never saved (read-only offline is a later feature).
//
// Each build saves its files under its own name ("app-<hash>") and removes the older copies once it takes over.

/** One file of the build, as vite-plugin-pwa lists them (`self.__WB_MANIFEST`). */
export interface PrecacheEntry {
  url: string
  revision: string | null
}

/** The few parts of a service worker's world this file uses, so tests can stand in for them. */
export interface WorkerScope {
  addEventListener(type: 'install' | 'activate', listener: (event: WaitUntilEvent) => void): void
  addEventListener(type: 'fetch', listener: (event: FetchLikeEvent) => void): void
  skipWaiting(): Promise<void>
  clients: { claim(): Promise<void> }
  location: { origin: string }
  caches: CacheStorage
  fetch(request: Request): Promise<Response>
}

export interface WaitUntilEvent {
  waitUntil(work: Promise<unknown>): void
}

export interface FetchLikeEvent {
  request: Request
  respondWith(response: Promise<Response>): void
}

/** The page every address of the app uses (a single-page app). */
export const appPage = '/index.html'

/** How long a page may take before the saved one is shown instead (a weak signal that never quite fails). */
export const pageTimeoutMs = 6000

/** "/assets/x.js" for "assets/x.js": addresses as the browser asks for them. */
export function precachePaths(entries: readonly PrecacheEntry[]): string[] {
  return entries.map((entry) => new URL(entry.url, 'https://app.invalid/').pathname)
}

/** A name that changes whenever any file of the build changes. */
export function cacheName(entries: readonly PrecacheEntry[]): string {
  const text = entries.map((entry) => `${entry.url}@${entry.revision ?? ''}`).join('\n')
  let hash = 5381
  for (let index = 0; index < text.length; index += 1) {
    hash = ((hash * 33) ^ text.charCodeAt(index)) >>> 0
  }
  return `app-${hash.toString(36)}`
}

/** The network's answer, or `null` when it fails or takes longer than `timeoutMs`. */
async function fromNetwork(
  fetchPage: () => Promise<Response>,
  timeoutMs: number,
): Promise<Response | null> {
  let timer: ReturnType<typeof setTimeout> | undefined
  const late = new Promise<null>((resolve) => {
    timer = setTimeout(() => resolve(null), timeoutMs)
  })
  try {
    return await Promise.race([fetchPage().catch(() => null), late])
  } finally {
    clearTimeout(timer)
  }
}

export function startServiceWorker(
  scope: WorkerScope,
  entries: readonly PrecacheEntry[],
  { timeoutMs = pageTimeoutMs }: { timeoutMs?: number } = {},
): void {
  const name = cacheName(entries)
  const paths = precachePaths(entries)
  const saved = new Set(paths)

  scope.addEventListener('install', (event) => {
    event.waitUntil(
      scope.caches
        .open(name)
        // `reload`: straight from the server, never from the browser's own cache of an older build.
        .then((cache) =>
          cache.addAll(
            paths.map(
              (path) => new Request(new URL(path, scope.location.origin), { cache: 'reload' }),
            ),
          ),
        )
        .then(() => scope.skipWaiting()),
    )
  })

  scope.addEventListener('activate', (event) => {
    event.waitUntil(
      scope.caches
        .keys()
        .then((keys) =>
          Promise.all(
            keys
              .filter((key) => key.startsWith('app-') && key !== name)
              .map((key) => scope.caches.delete(key)),
          ),
        )
        .then(() => scope.clients.claim()),
    )
  })

  scope.addEventListener('fetch', (event) => {
    const { request } = event
    if (request.method !== 'GET') {
      return
    }
    const url = new URL(request.url)
    if (url.origin !== scope.location.origin) {
      return
    }
    if (request.mode === 'navigate') {
      event.respondWith(page(request))
    } else if (saved.has(url.pathname)) {
      event.respondWith(file(request, url.pathname))
    }
  })

  async function page(request: Request): Promise<Response> {
    const response = await fromNetwork(() => scope.fetch(request), timeoutMs)
    if (response) {
      return response
    }
    const savedPage = await scope.caches.match(appPage, { cacheName: name })
    return savedPage ?? Response.error()
  }

  async function file(request: Request, path: string): Promise<Response> {
    const savedFile = await scope.caches.match(path, { cacheName: name })
    return savedFile ?? scope.fetch(request)
  }
}
