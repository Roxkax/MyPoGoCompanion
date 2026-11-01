import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import type { Me, SessionResponse } from '../auth/authApi'
import { AuthProvider } from '../auth/AuthProvider'
import { SessionManager, type SessionDependencies } from '../auth/session'

/** A Storage kept in memory, so tests don't share the browser's. */
export class MemoryStorage implements Storage {
  private readonly items = new Map<string, string>()
  get length() {
    return this.items.size
  }
  clear() {
    this.items.clear()
  }
  getItem(key: string) {
    return this.items.get(key) ?? null
  }
  key(index: number) {
    return [...this.items.keys()][index] ?? null
  }
  removeItem(key: string) {
    this.items.delete(key)
  }
  setItem(key: string, value: string) {
    this.items.set(key, value)
  }
}

export const ash: Me = {
  id: '0190e0e0-0000-7000-8000-000000000001',
  email: 'ash@example.com',
  name: 'Ash',
  pictureUrl: null,
  role: 'User',
  language: 'en',
  theme: 'Device',
}

export function sessionFor(
  user: Me = ash,
  refreshToken = 'refresh-2',
  minutes = 15,
): SessionResponse {
  return {
    accessToken: `access-for-${refreshToken}`,
    accessTokenExpiresAt: new Date(Date.now() + minutes * 60_000).toISOString(),
    refreshToken,
    user,
  }
}

/** A real SessionManager on memory storage with a stand-in API (signed out unless told otherwise). */
export function testSession(deps: Partial<SessionDependencies> = {}) {
  const storage = deps.storage ?? new MemoryStorage()
  const api = {
    refreshSession: async () => {
      throw new Error('no refresh in this test')
    },
    signOut: async () => {},
    signOutEverywhere: async () => {},
    ...deps.api,
  }
  return { session: new SessionManager({ storage, locks: undefined, ...deps, api }), storage, api }
}

export function renderWithAuth(
  ui: ReactElement,
  options: { session?: SessionManager; navigate?: (url: string) => void; pathname?: string } = {},
) {
  const session = options.session ?? testSession().session
  const result = render(
    <QueryClientProvider client={new QueryClient()}>
      <AuthProvider
        session={session}
        navigate={options.navigate ?? (() => {})}
        pathname={options.pathname ?? '/'}
      >
        {ui}
      </AuthProvider>
    </QueryClientProvider>,
  )
  return { ...result, session }
}

/** fetch answering by path: each entry is the response for URLs ending with that path. */
export function routeFetch(routes: Record<string, () => Response | Promise<Response>>) {
  return (input: RequestInfo | URL) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url
    const match = Object.keys(routes).find((path) => url.endsWith(path))
    return match
      ? Promise.resolve(routes[match]!())
      : Promise.reject(new Error(`unexpected fetch ${url}`))
  }
}
