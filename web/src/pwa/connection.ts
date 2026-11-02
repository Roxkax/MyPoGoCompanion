import type { Middleware } from 'openapi-fetch'
import { apiBaseUrl } from '../api/client'

// Whether the device has a connection, for the "You're offline" screen (H3, E01-F06), and whether the API answers,
// for "MyPoGoCompanion isn't answering" (E01-F08). The app shows "offline" when the browser says it's offline, or when
// a request to the API fails and even the app's own site can't be reached. If the site answers but the API's liveness
// check doesn't, the internet works and only the API is in trouble: the API screen. While either shows, the app
// checks again every few seconds, when the browser says it's back and on Try again; once everything answers it
// reloads the app, which then gets everything fresh (decisions 2026-11-01 and 2026-11-02).

/** An address on the app's own site. Any answer at all (even "not found") means there is a connection. */
export const onlineCheckPath = '/online-check'

/** How often to check again while offline. */
export const retryEveryMs = 10_000

/** How long a check may take before it counts as "no connection". */
export const checkTimeoutMs = 5_000

/** At most one check every this often after failed API requests (an API that is down fails every request). */
export const errorCheckGapMs = 5_000

/** The API's liveness check: answers as long as the API runs, without touching the database. */
export const apiCheckPath = '/health/live'

export interface ConnectionState {
  offline: boolean
  /** The internet works but the API doesn't answer. */
  apiDown: boolean
  /** A check is running (Try again shows "Checking…"). */
  checking: boolean
  /** Try again found the same problem (still offline, or the API still not answering). */
  stillFailing: boolean
  /** While the API is down: the failed answer's reference, when it had one (no answer at all has none). */
  reference: string | null
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

/** Asks the API's liveness check, bypassing every cache: true only for a good answer. */
export async function reachesApi(
  fetchApi: typeof fetch = (...args) => globalThis.fetch(...args),
  baseUrl: string = apiBaseUrl(),
  timeoutMs: number = checkTimeoutMs,
): Promise<boolean> {
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), timeoutMs)
  try {
    const response = await fetchApi(`${baseUrl}${apiCheckPath}`, {
      cache: 'no-store',
      signal: controller.signal,
    })
    return response.ok
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
  /** Whether the app's own site answers (the internet works). */
  check?: () => Promise<boolean>
  /** Whether the API answers. */
  checkApi?: () => Promise<boolean>
  reload?: () => void
  now?: () => number
}

export class ConnectionWatch {
  private state: ConnectionState
  private readonly listeners = new Set<Listener>()
  private readonly environment: ConnectionEnvironment
  private readonly checkSite: () => Promise<boolean>
  private readonly checkApi: () => Promise<boolean>
  private readonly reload: () => void
  private readonly now: () => number
  private retry: ReturnType<typeof setInterval> | undefined
  private lastErrorCheck = Number.NEGATIVE_INFINITY

  constructor(options: ConnectionOptions = {}) {
    this.environment = options.environment ?? (window as unknown as ConnectionEnvironment)
    this.checkSite = options.check ?? (() => reachesSite())
    this.checkApi = options.checkApi ?? (() => reachesApi())
    this.reload = options.reload ?? (() => window.location.reload())
    this.now = options.now ?? Date.now
    this.state = {
      offline: !this.environment.navigator.onLine,
      apiDown: false,
      checking: false,
      stillFailing: false,
      reference: null,
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
      if (this.failing() && environment.document.visibilityState === 'visible') {
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

  /**
   * A request got no answer at all (or the gateway's "bad gateway" / "unavailable" / "timed out"): find out whether the
   * connection is gone or only the API. `reference` is the failed answer's, when it had one.
   */
  reportNetworkError(reference: string | null = null): void {
    if (this.failing() || this.state.checking) {
      return
    }
    if (this.now() - this.lastErrorCheck < errorCheckGapMs) {
      return
    }
    this.lastErrorCheck = this.now()
    void this.diagnose(reference)
  }

  /** Try again on the offline or API screen. */
  tryAgain(): Promise<void> {
    return this.check(true)
  }

  /** QA and local copies only (`?fail=api`): shows the API screen as if the API had stopped answering. */
  pretendApiDown(reference: string | null = null): void {
    this.goApiDown(reference, false)
  }

  private failing(): boolean {
    return this.state.offline || this.state.apiDown
  }

  /** After a failed request: is it the connection, the API, or neither (a one-off)? */
  private async diagnose(reference: string | null): Promise<void> {
    this.update({ checking: true })
    if (!(await this.checkSite())) {
      this.goOffline(false)
    } else if (!(await this.checkApi())) {
      this.goApiDown(reference, false)
    } else {
      this.update({ checking: false })
    }
  }

  private async check(byHand = false): Promise<void> {
    if (this.state.checking) {
      return
    }
    this.update({ checking: true })
    if (!(await this.checkSite())) {
      this.goOffline(byHand)
    } else if (this.state.apiDown && !(await this.checkApi())) {
      this.goApiDown(this.state.reference, byHand)
    } else if (this.failing()) {
      // Back: the app starts again with everything fresh. Stays "checking" until the page goes.
      this.stopChecking()
      this.reload()
    } else {
      this.update({ checking: false })
    }
  }

  private goOffline(byHand: boolean): void {
    this.update({
      offline: true,
      apiDown: false,
      checking: false,
      stillFailing: byHand,
      reference: null,
    })
    this.keepChecking()
  }

  private goApiDown(reference: string | null, byHand: boolean): void {
    this.update({ offline: false, apiDown: true, checking: false, stillFailing: byHand, reference })
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

/** Answers that come from Azure's gateway rather than the API itself: the API isn't running or answering. */
export function isGatewayTrouble(response: Response): boolean {
  if (response.status === 502 || response.status === 504) {
    return true
  }
  // The API's own 503 (e.g. "sign-in isn't set up") is problem details; the gateway's isn't.
  return (
    response.status === 503 &&
    !(response.headers.get('Content-Type') ?? '').includes('application/problem+json')
  )
}

/**
 * Tells the watch when an API request fails without any answer (fetch throws a TypeError: no connection, DNS, CORS…)
 * or the gateway answers instead of the API.
 */
export function connectionMiddleware(
  watch: Pick<ConnectionWatch, 'reportNetworkError'>,
): Middleware {
  return {
    onResponse({ response }) {
      if (isGatewayTrouble(response)) {
        watch.reportNetworkError()
      }
      return undefined
    },
    onError({ error }) {
      if (error instanceof TypeError) {
        watch.reportNetworkError()
      }
      return undefined
    },
  }
}
