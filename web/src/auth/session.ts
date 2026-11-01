import * as api from './authApi'
import { ApiError, type Me, type SessionResponse } from './authApi'
import { readRefreshToken, refreshTokenKey, writeRefreshToken } from './storage'

export type AuthState =
  { status: 'loading' } | { status: 'signedOut' } | { status: 'signedIn'; user: Me }

/** What the session needs from the outside world, replaceable in tests. */
export interface SessionDependencies {
  api: Pick<typeof api, 'refreshSession' | 'signOut' | 'signOutEverywhere'>
  storage: Storage
  now: () => number
  /** The Web Locks API, so only one tab refreshes at a time (each refresh replaces the stored token). */
  locks?: Pick<LockManager, 'request'>
}

const lockName = 'mpgc-refresh'
/** Refresh a little before the access token runs out, not after. */
const earlyRefreshMs = 60_000

/**
 * The signed-in session in this browser. The refresh token is in localStorage, the access token only in memory.
 * Refreshes run one tab at a time under a Web Lock and always use the latest stored token, because each refresh
 * replaces it and presenting a replaced token ends the sign-in (the API treats it as stolen).
 */
export class SessionManager {
  private state: AuthState = { status: 'loading' }
  private accessToken: string | null = null
  private accessTokenExpiresAt = 0
  private readonly listeners = new Set<() => void>()
  private refreshing: Promise<boolean> | null = null
  private readonly deps: SessionDependencies

  constructor(deps: Partial<SessionDependencies> = {}) {
    this.deps = {
      api,
      storage: localStorage,
      now: () => Date.now(),
      locks: typeof navigator !== 'undefined' ? navigator.locks : undefined,
      ...deps,
    }
  }

  getState = (): AuthState => this.state

  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }

  /** On page load: signed out without a stored refresh token, otherwise swap it for a fresh session. */
  async start(): Promise<void> {
    if (this.state.status === 'signedIn') {
      return
    }
    if (!readRefreshToken(this.deps.storage)) {
      this.setState({ status: 'signedOut' })
      return
    }
    await this.refresh()
  }

  /** Takes over a session the API just issued (after the Google sign-in or a refresh). */
  accept(session: SessionResponse): void {
    writeRefreshToken(session.refreshToken, this.deps.storage)
    this.accessToken = session.accessToken
    this.accessTokenExpiresAt = Date.parse(session.accessTokenExpiresAt)
    this.setState({ status: 'signedIn', user: session.user })
  }

  /** A valid access token for an API call, refreshing first when it is about to run out; null when signed out. */
  async getAccessToken(): Promise<string | null> {
    if (this.accessToken && this.accessTokenExpiresAt - earlyRefreshMs > this.deps.now()) {
      return this.accessToken
    }
    return (await this.refresh()) ? this.accessToken : null
  }

  /** Swaps the stored refresh token for a new session. False when the person has to sign in again. */
  refresh(): Promise<boolean> {
    this.refreshing ??= this.withLock(() => this.refreshNow()).finally(
      () => (this.refreshing = null),
    )
    return this.refreshing
  }

  /** Signs out on this device: forgets the tokens straight away, then tells the API (best effort). */
  async signOut(): Promise<void> {
    const token = readRefreshToken(this.deps.storage)
    this.forget()
    if (token) {
      try {
        await this.deps.api.signOut(token)
      } catch {
        // Already signed out here; the API forgets the token when it expires.
      }
    }
  }

  /** Ends every sign-in of this account, on every device, then signs out here. */
  async signOutEverywhere(): Promise<void> {
    const accessToken = await this.getAccessToken()
    if (accessToken) {
      await this.deps.api.signOutEverywhere(accessToken)
    }
    this.forget()
  }

  /** Another tab signed out (it removed the stored token): follow it. */
  handleStorageEvent = (event: Pick<StorageEvent, 'key' | 'newValue'>): void => {
    if (
      event.key === refreshTokenKey &&
      event.newValue === null &&
      this.state.status === 'signedIn'
    ) {
      this.forget()
    }
  }

  private async refreshNow(): Promise<boolean> {
    // Read inside the lock: another tab may have just replaced the token.
    const token = readRefreshToken(this.deps.storage)
    if (!token) {
      this.forget()
      return false
    }
    try {
      this.accept(await this.deps.api.refreshSession(token))
      return true
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        this.forget()
      } else if (this.state.status === 'loading') {
        // The API couldn't be reached: show signed out for now but keep the token for the next try.
        this.setState({ status: 'signedOut' })
      }
      return false
    }
  }

  private withLock<T>(action: () => Promise<T>): Promise<T> {
    return this.deps.locks ? (this.deps.locks.request(lockName, action) as Promise<T>) : action()
  }

  private forget(): void {
    writeRefreshToken(null, this.deps.storage)
    this.accessToken = null
    this.accessTokenExpiresAt = 0
    this.setState({ status: 'signedOut' })
  }

  private setState(state: AuthState): void {
    this.state = state
    this.listeners.forEach((listener) => listener())
  }
}
