import { apiUrl } from '../api/health'

// The sign-in endpoints, written by hand until the generated OpenAPI client arrives (E01-F05).

export type Role = 'User' | 'Admin' | 'SuperAdmin'

export interface Me {
  id: string
  email: string
  name: string
  pictureUrl: string | null
  role: Role
  language: string
  theme: 'Device' | 'Light' | 'Dark'
}

export interface SessionResponse {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  user: Me
}

/** A request the API refused, with its status (401 means "sign in again"). */
export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function send<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(apiUrl(path), {
    ...init,
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', ...init.headers },
  })
  if (!response.ok) {
    let detail = `The API answered ${response.status}`
    try {
      const problem = (await response.json()) as { detail?: string }
      detail = problem.detail ?? detail
    } catch {
      // No problem details in the body: keep the status message.
    }
    throw new ApiError(response.status, detail)
  }
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

export async function fetchGoogleClientId(): Promise<string> {
  return (await send<{ clientId: string }>('/auth/google/client')).clientId
}

export function signInWithGoogle(
  code: string,
  codeVerifier: string,
  redirectUri: string,
): Promise<SessionResponse> {
  return send('/auth/google', {
    method: 'POST',
    body: JSON.stringify({ code, codeVerifier, redirectUri }),
  })
}

export function refreshSession(refreshToken: string): Promise<SessionResponse> {
  return send('/auth/refresh', { method: 'POST', body: JSON.stringify({ refreshToken }) })
}

export function signOut(refreshToken: string): Promise<void> {
  return send('/auth/sign-out', { method: 'POST', body: JSON.stringify({ refreshToken }) })
}

export function signOutEverywhere(accessToken: string): Promise<void> {
  return send('/auth/sign-out-everywhere', {
    method: 'POST',
    headers: { Authorization: `Bearer ${accessToken}` },
  })
}
