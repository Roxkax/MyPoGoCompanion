import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  ApiError,
  fetchGoogleClientId,
  refreshSession,
  saveLanguage,
  signInWithGoogle,
  signOut,
  signOutEverywhere,
} from './authApi'
import { readRequest, type SentRequest } from '../testing/fetch'

afterEach(() => vi.unstubAllGlobals())

function capture(response: Response) {
  const calls: SentRequest[] = []
  vi.stubGlobal('fetch', async (input: RequestInfo | URL, init?: RequestInit) => {
    calls.push(await readRequest(input, init))
    return response
  })
  return calls
}

describe('sign-in API', () => {
  it('reads the Google client id', async () => {
    capture(Response.json({ clientId: 'id.apps.googleusercontent.com' }))

    expect(await fetchGoogleClientId()).toBe('id.apps.googleusercontent.com')
  })

  it('posts the code, verifier and return address', async () => {
    const calls = capture(Response.json({ accessToken: 'a' }))

    await signInWithGoogle('code', 'verifier', 'https://web.example/auth/callback')

    expect(calls[0]!.url).toMatch(/\/auth\/google$/)
    expect(calls[0]!.method).toBe('POST')
    expect(calls[0]!.body).toEqual({
      code: 'code',
      codeVerifier: 'verifier',
      redirectUri: 'https://web.example/auth/callback',
    })
  })

  it('sends the device’s language with the sign-in, for a new account', async () => {
    const calls = capture(Response.json({ accessToken: 'a' }))

    await signInWithGoogle('code', 'verifier', 'https://web.example/auth/callback', 'pt-PT')

    expect(calls[0]!.body).toMatchObject({ language: 'pt-PT' })
  })

  it('saves the language with the account', async () => {
    const calls = capture(new Response(null, { status: 204 }))

    await saveLanguage('access', 'pt-PT')

    expect(calls[0]!.url).toMatch(/\/me\/language$/)
    expect(calls[0]!.method).toBe('PUT')
    expect(calls[0]!.headers.get('Authorization')).toBe('Bearer access')
    expect(calls[0]!.body).toEqual({ language: 'pt-PT' })
  })

  it('refreshes and signs out with the refresh token in the body', async () => {
    const calls = capture(new Response(null, { status: 204 }))

    await signOut('rt')
    expect(calls[0]!.url).toMatch(/\/auth\/sign-out$/)
    expect(calls[0]!.body).toEqual({ refreshToken: 'rt' })

    capture(Response.json({ accessToken: 'a' }))
    expect(await refreshSession('rt')).toEqual({ accessToken: 'a' })
  })

  it('signs out everywhere with the access token as a bearer token', async () => {
    const calls = capture(new Response(null, { status: 204 }))

    await signOutEverywhere('access')

    expect(calls[0]!.url).toMatch(/\/auth\/sign-out-everywhere$/)
    expect(calls[0]!.headers.get('Authorization')).toBe('Bearer access')
  })

  it('turns a refusal into an ApiError with the problem detail', async () => {
    capture(Response.json({ detail: "Sign-in isn't set up on this server yet." }, { status: 503 }))

    const error = await fetchGoogleClientId().catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({
      status: 503,
      message: "Sign-in isn't set up on this server yet.",
    })
  })

  it('keeps the status message when the refusal has no body', async () => {
    capture(new Response('oops', { status: 401 }))

    await expect(refreshSession('rt')).rejects.toMatchObject({
      status: 401,
      message: 'The API answered 401',
    })
  })
})
