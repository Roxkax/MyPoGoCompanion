import { describe, expect, it, vi } from 'vitest'
import { ash, MemoryStorage, sessionFor, testSession } from '../testing/auth'
import { ApiError } from './authApi'
import { refreshTokenKey } from './storage'

describe('SessionManager', () => {
  it('starts signed out without a stored refresh token', async () => {
    const { session } = testSession()

    await session.start()

    expect(session.getState()).toEqual({ status: 'signedOut' })
  })

  it('starts signed in by swapping the stored refresh token', async () => {
    const storage = new MemoryStorage()
    storage.setItem(refreshTokenKey, 'refresh-1')
    const refreshSession = vi.fn(async () => sessionFor(ash, 'refresh-2'))
    const { session } = testSession({ storage, api: { refreshSession } as never })

    await session.start()

    expect(refreshSession).toHaveBeenCalledWith('refresh-1')
    expect(session.getState()).toEqual({ status: 'signedIn', user: ash })
    expect(storage.getItem(refreshTokenKey)).toBe('refresh-2')
    expect(await session.getAccessToken()).toBe('access-for-refresh-2')
  })

  it('forgets a refresh token the API refuses', async () => {
    const storage = new MemoryStorage()
    storage.setItem(refreshTokenKey, 'stolen')
    const { session } = testSession({
      storage,
      api: { refreshSession: async () => Promise.reject(new ApiError(401, 'no')) } as never,
    })

    await session.start()

    expect(session.getState()).toEqual({ status: 'signedOut' })
    expect(storage.getItem(refreshTokenKey)).toBeNull()
  })

  it('keeps the token when the API is unreachable, showing signed out for now', async () => {
    const storage = new MemoryStorage()
    storage.setItem(refreshTokenKey, 'refresh-1')
    const { session } = testSession({
      storage,
      api: { refreshSession: async () => Promise.reject(new TypeError('offline')) } as never,
    })

    await session.start()

    expect(session.getState()).toEqual({ status: 'signedOut' })
    expect(storage.getItem(refreshTokenKey)).toBe('refresh-1')
  })

  it('keeps a signed-in state through a failed background refresh', async () => {
    const refreshSession = vi.fn().mockRejectedValue(new TypeError('offline'))
    const { session } = testSession({ api: { refreshSession } as never })
    session.accept(sessionFor(ash, 'r', 0))

    expect(await session.getAccessToken()).toBeNull()
    expect(session.getState()).toEqual({ status: 'signedIn', user: ash })
  })

  it('refreshes shortly before the access token runs out, once for simultaneous callers', async () => {
    const refreshSession = vi.fn(async () => sessionFor(ash, 'refresh-3'))
    const { session } = testSession({ api: { refreshSession } as never })
    session.accept(sessionFor(ash, 'refresh-2', 0.5))

    const [a, b] = await Promise.all([session.getAccessToken(), session.getAccessToken()])

    expect(refreshSession).toHaveBeenCalledTimes(1)
    expect(refreshSession).toHaveBeenCalledWith('refresh-2')
    expect([a, b]).toEqual(['access-for-refresh-3', 'access-for-refresh-3'])
  })

  it('uses a fresh access token without calling the API', async () => {
    const refreshSession = vi.fn()
    const { session } = testSession({ api: { refreshSession } as never })
    session.accept(sessionFor())

    expect(await session.getAccessToken()).toBe('access-for-refresh-2')
    expect(refreshSession).not.toHaveBeenCalled()
  })

  it('refreshes under the Web Lock, reading the token another tab stored', async () => {
    const storage = new MemoryStorage()
    const refreshSession = vi.fn(async (token: string) => sessionFor(ash, `${token}-next`))
    const locks = {
      request: vi.fn(async (_name: string, callback: () => Promise<unknown>) => {
        storage.setItem(refreshTokenKey, 'from-other-tab')
        return callback()
      }),
    }
    const { session } = testSession({
      storage,
      locks: locks as never,
      api: { refreshSession } as never,
    })
    session.accept(sessionFor(ash, 'mine', 0))

    await session.refresh()

    expect(locks.request).toHaveBeenCalledWith('mpgc-refresh', expect.any(Function))
    expect(refreshSession).toHaveBeenCalledWith('from-other-tab')
  })

  it('signs out when there is nothing to refresh', async () => {
    const { session } = testSession()
    session.accept(sessionFor())
    session['deps'].storage.removeItem(refreshTokenKey)

    expect(await session.refresh()).toBe(false)
    expect(session.getState()).toEqual({ status: 'signedOut' })
  })

  it('signs out here at once and tells the API, ignoring its errors', async () => {
    const signOut = vi.fn().mockRejectedValue(new Error('offline'))
    const { session, storage } = testSession({ api: { signOut } as never })
    session.accept(sessionFor(ash, 'refresh-2'))

    await session.signOut()

    expect(signOut).toHaveBeenCalledWith('refresh-2')
    expect(storage.getItem(refreshTokenKey)).toBeNull()
    expect(session.getState()).toEqual({ status: 'signedOut' })
  })

  it('signing out without a token calls nothing', async () => {
    const signOut = vi.fn()
    const { session } = testSession({ api: { signOut } as never })

    await session.signOut()

    expect(signOut).not.toHaveBeenCalled()
  })

  it('signs out everywhere with the access token, then here', async () => {
    const signOutEverywhere = vi.fn(async () => {})
    const { session } = testSession({ api: { signOutEverywhere } as never })
    session.accept(sessionFor())

    await session.signOutEverywhere()

    expect(signOutEverywhere).toHaveBeenCalledWith('access-for-refresh-2')
    expect(session.getState()).toEqual({ status: 'signedOut' })
  })

  it('signing out everywhere while signed out just stays signed out', async () => {
    const signOutEverywhere = vi.fn()
    const { session } = testSession({ api: { signOutEverywhere } as never })

    await session.signOutEverywhere()

    expect(signOutEverywhere).not.toHaveBeenCalled()
  })

  it('follows another tab that signed out', () => {
    const { session } = testSession()
    session.accept(sessionFor())
    const listener = vi.fn()
    session.subscribe(listener)

    session.handleStorageEvent({ key: 'other', newValue: null })
    session.handleStorageEvent({ key: refreshTokenKey, newValue: 'rotated' })
    expect(session.getState().status).toBe('signedIn')

    session.handleStorageEvent({ key: refreshTokenKey, newValue: null })
    expect(session.getState()).toEqual({ status: 'signedOut' })
    expect(listener).toHaveBeenCalledTimes(1)
  })

  it('stops notifying after unsubscribing', () => {
    const { session } = testSession()
    const listener = vi.fn()
    session.subscribe(listener)()

    session.accept(sessionFor())

    expect(listener).not.toHaveBeenCalled()
  })

  it('uses the browser defaults when given nothing', async () => {
    localStorage.clear()
    const { SessionManager } = await import('./session')

    const session = new SessionManager()
    await session.start()

    expect(session.getState()).toEqual({ status: 'signedOut' })
  })
})
