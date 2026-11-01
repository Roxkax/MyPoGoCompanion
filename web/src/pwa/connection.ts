import type { Middleware } from 'openapi-fetch'

// Whether the device has a connection, for the "You're offline" screen (H3, E01-F06). The app shows it when the
// browser says it's offline, or when a request to the API fails and even the app's own site can't be reached. If the
// site answers, the internet works and only the API is in trouble: that's an API error, not "offline" (E01-F08).
// While offline it checks again every few seconds, when the browser says it's back and on Try again; once the site
// answers it reloads the app, which then gets everything fresh (decision 2026-11-01).

/** An address on the app's own site. Any answer at all (even "not found") means there is a connection. */
export const onlineCheckPath = '/online-check'

/** How often to check again while offline. */
export const retryEveryMs = 10_000

/** How long a check may take before it counts as "no connection". */
export const checkTimeoutMs = 5_000

/** At most one check every this often after failed API requests (an API that is down fails every request). */
export const errorCheckGapMs = 5_000

export interface ConnectionState {
  offline: boolean
  /** A check is running (Try again shows "Checking…"). */
  checking: boolean
  /** Try again found no connection. */
  stillOffline: boolean
}

/** Asks the app's own site for anything, bypassing every cache: true when it answers. */
export async function reachesSite(
  fetchSite: typeof fetch = (...args) => globalThis.fetch(...args),
  timeoutMs: number = checkTimeoutMs,
  now: () => number = Date.now,
): Promise<boolean> {
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), timeoutMs)
  try {
    await fetchSite(`${onlineCheckPath}?at=${now()}`, {
      method: 'HEAD',
      cache: 'no-store',
      signal: controller.signal,
    })
    return true
  } catch {
    return false
  } finally {
    clearTimeout(timer)
  }
}

type Listener = () => void

export interface ConnectionEnvironment {
  navigator: { onLine: boolean }
  addEventListener(type: 'online' | 'offline', listener: Listener): void
  removeEventListener(type: 'online' | 'offline', listener: Listener): void
  document: Pick<Document, 'visibilityState' | 'addEventListener' | 'removeEventListener'>
}

export interface ConnectionOptions {
  environment?: ConnectionEnvironment
  check?: () => Promise<boolean>
  reload?: () => void
  now?: () => number
}

export class ConnectionWatch {
  private state: ConnectionState
  private readonly listeners = new Set<Listener>()
  private readonly environment: ConnectionEnvironment
  private readonly checkSite: () => Promise<boolean>
  private readonly reload: () => void
  private readonly now: () => number
  private retry: ReturnType<typeof setInterval> | undefined
  private lastErrorCheck = Number.NEGATIVE_INFINITY

  constructor(options: ConnectionOptions = {}) {
    this.environment = options.environment ?? (window as unknown as ConnectionEnvironment)
    this.checkSite = options.check ?? (() => reachesSite())
    this.reload = options.reload ?? (() => window.location.reload())
    this.now = options.now ?? Date.now
    this.state = {
      offline: !this.environment.navigator.onLine,
      checking: false,
      stillOffline: false,
    }
  }

  readonly getState = (): ConnectionState => this.state

  readonly subscribe = (listener: Listener): (() => void) => {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }

  /** Listens to the browser; returns a function that stops listening. */
  start(): () => void {
    const { environment } = this
    const onOffline = () => this.goOffline(false)
    const onOnline = () => void this.check()
    const onVisible = () => {
      if (this.state.offline && environment.document.visibilityState === 'visible') {
        void this.check()
      }
    }
    environment.addEventListener('offline', onOffline)
    environment.addEventListener('online', onOnline)
    environment.document.addEventListener('visibilitychange', onVisible)
    if (this.state.offline) {
      this.keepChecking()
    }
    return () => {
      environment.removeEventListener('offline', onOffline)
      environment.removeEventListener('online', onOnline)
      environment.document.removeEventListener('visibilitychange', onVisible)
      this.stopChecking()
    }
  }

  /** A request couldn't reach the API at all: find out whether the connection is gone. */
  reportNetworkError(): void {
    if (this.state.offline || this.state.checking) {
      return
    }
    if (this.now() - this.lastErrorCheck < errorCheckGapMs) {
      return
    }
    this.lastErrorCheck = this.now()
    void this.check()
  }

  /** Try again on the offline screen. */
  tryAgain(): Promise<void> {
    return this.check(true)
  }

  private async check(byHand = false): Promise<void> {
    if (this.state.checking) {
      return
    }
    this.update({ checking: true })
    const connected = await this.checkSite()
    if (!connected) {
      this.goOffline(byHand)
    } else if (this.state.offline) {
      // Back online: the app starts again with everything fresh. Stays "checking" until the page goes.
      this.stopChecking()
      this.reload()
    } else {
      this.update({ checking: false })
    }
  }

  private goOffline(byHand: boolean): void {
    this.update({ offline: true, checking: false, stillOffline: byHand })
    this.keepChecking()
  }

  private keepChecking(): void {
    this.retry ??= setInterval(() => void this.check(), retryEveryMs)
  }

  private stopChecking(): void {
    clearInterval(this.retry)
    this.retry = undefined
  }

  private update(change: Partial<ConnectionState>): void {
    this.state = { ...this.state, ...change }
    this.listeners.forEach((listener) => listener())
  }
}

/** Tells the watch when an API request fails without any answer (fetch throws a TypeError: no connection, DNS…). */
export function connectionMiddleware(
  watch: Pick<ConnectionWatch, 'reportNetworkError'>,
): Middleware {
  return {
    onError({ error }) {
      if (error instanceof TypeError) {
        watch.reportNetworkError()
      }
      return undefined
    },
  }
}
