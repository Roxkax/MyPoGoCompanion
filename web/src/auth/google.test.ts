import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { routeFetch, sessionFor } from '../testing/auth'
import {
  buildGoogleSignInUrl,
  completeGoogleSignIn,
  redirectUri,
  safeReturnTo,
  SignInError,
} from './google'
import { challengeFor } from './pkce'
import { writePendingSignIn } from './storage'

const origin = 'https://web.example'

beforeEach(() => sessionStorage.clear())
afterEach(() => vi.unstubAllGlobals())

describe('Google sign-in', () => {
  it('sends people to Google with our client, PKCE and a state, remembering where they were', async () => {
    vi.stubGlobal(
      'fetch',
      routeFetch({ '/auth/google/client': () => Response.json({ clientId: 'cid' }) }),
    )

    const url = new URL(await buildGoogleSignInUrl('/pokedex?type=fire', origin))

    expect(url.origin + url.pathname).toBe('https://accounts.google.com/o/oauth2/v2/auth')
    const pending = JSON.parse(sessionStorage.getItem('mpgc.pendingSignIn')!)
    expect(Object.fromEntries(url.searchParams)).toEqual({
      client_id: 'cid',
      redirect_uri: 'https://web.example/auth/callback',
      response_type: 'code',
      scope: 'openid email profile',
      state: pending.state,
      code_challenge: await challengeFor(pending.verifier),
      code_challenge_method: 'S256',
      prompt: 'select_account',
    })
    expect(pending.returnTo).toBe('/pokedex?type=fire')
  })

  it.each([
    ['/raids', '/raids'],
    ['//evil.example', '/'],
    ['/\\evil.example', '/'],
    ['https://evil.example', '/'],
    [null, '/'],
  ])('only returns to a path on this site: %s → %s', (value, expected) => {
    expect(safeReturnTo(value)).toBe(expected)
  })

  it('uses the page origin by default', () => {
    expect(redirectUri()).toBe(`${window.location.origin}/auth/callback`)
  })

  it('finishes with the API when the state matches', async () => {
    writePendingSignIn({ state: 'abc', verifier: 'v'.repeat(64), returnTo: '/collection' })
    const posted: unknown[] = []
    vi.stubGlobal('fetch', (_url: string, init: RequestInit) => {
      posted.push(JSON.parse(init.body as string))
      return Promise.resolve(Response.json(sessionFor()))
    })

    const result = await completeGoogleSignIn('?code=the-code&state=abc', origin)

    expect(result.returnTo).toBe('/collection')
    expect(posted).toEqual([
      {
        code: 'the-code',
        codeVerifier: 'v'.repeat(64),
        redirectUri: 'https://web.example/auth/callback',
      },
    ])
  })

  it.each([
    ['?code=c&state=other', 'This sign-in link has expired. Please try again.'],
    ['?state=abc', 'This sign-in link has expired. Please try again.'],
    ['?error=access_denied&state=abc', 'Sign-in was cancelled.'],
  ])('refuses %s', async (search, message) => {
    writePendingSignIn({ state: 'abc', verifier: 'v'.repeat(64), returnTo: '/' })

    const error = await completeGoogleSignIn(search, origin).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(SignInError)
    expect((error as Error).message).toBe(message)
  })

  it('refuses a callback nobody started in this tab', async () => {
    await expect(completeGoogleSignIn('?code=c&state=abc', origin)).rejects.toBeInstanceOf(
      SignInError,
    )
  })
})
