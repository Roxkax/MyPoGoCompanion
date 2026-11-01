// Where the sign-in keeps things in the browser. The refresh token lives in localStorage (decision 2026-11-01: the
// web app and the API are on different sites, so a cookie scripts can't read would be a blocked third-party cookie);
// the access token is only ever in memory. Storage can be unavailable (private mode, blocked site data): every access
// is guarded so the app still works signed out.

export const refreshTokenKey = 'mpgc.refreshToken'
export const pendingSignInKey = 'mpgc.pendingSignIn'

function safely<T>(action: () => T, fallback: T): T {
  try {
    return action()
  } catch {
    return fallback
  }
}

export function readRefreshToken(storage: Storage = localStorage): string | null {
  return safely(() => storage.getItem(refreshTokenKey), null)
}

export function writeRefreshToken(token: string | null, storage: Storage = localStorage): void {
  safely(
    () => (token ? storage.setItem(refreshTokenKey, token) : storage.removeItem(refreshTokenKey)),
    undefined,
  )
}

/** What the browser remembers between leaving for Google and coming back (this tab only). */
export interface PendingSignIn {
  state: string
  verifier: string
  returnTo: string
}

export function writePendingSignIn(
  pending: PendingSignIn,
  storage: Storage = sessionStorage,
): void {
  safely(() => storage.setItem(pendingSignInKey, JSON.stringify(pending)), undefined)
}

/** Reads and forgets it: each sign-in attempt can be completed once. */
export function takePendingSignIn(storage: Storage = sessionStorage): PendingSignIn | null {
  return safely(() => {
    const value = storage.getItem(pendingSignInKey)
    storage.removeItem(pendingSignInKey)
    const parsed = value ? (JSON.parse(value) as Partial<PendingSignIn>) : null
    return parsed &&
      typeof parsed.state === 'string' &&
      typeof parsed.verifier === 'string' &&
      typeof parsed.returnTo === 'string'
      ? { state: parsed.state, verifier: parsed.verifier, returnTo: parsed.returnTo }
      : null
  }, null)
}
