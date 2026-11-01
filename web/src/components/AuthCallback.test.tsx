import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { writePendingSignIn } from '../auth/storage'
import { LanguageStore } from '../i18n/language'
import {
  ash,
  MemoryStorage,
  renderWithAuth,
  routeFetch,
  sessionFor,
  testSession,
} from '../testing/auth'
import { AuthCallback } from './AuthCallback'
import { SignInDialog } from './SignInDialog'

beforeEach(() => sessionStorage.clear())
afterEach(() => {
  vi.unstubAllGlobals()
  window.history.replaceState(null, '', '/')
})

describe('AuthCallback', () => {
  it('signs in and returns to the page the person started from', async () => {
    writePendingSignIn({ state: 'abc', verifier: 'v'.repeat(64), returnTo: '/collection' })
    window.history.replaceState(null, '', '/auth/callback?code=c&state=abc')
    vi.stubGlobal('fetch', routeFetch({ '/auth/google': () => Response.json(sessionFor()) }))
    const onDone = vi.fn()
    const { session } = testSession()

    renderWithAuth(<AuthCallback onDone={onDone} />, { session, pathname: '/auth/callback' })

    expect(screen.getByRole('status')).toHaveTextContent('Signing you in…')
    await vi.waitFor(() => expect(onDone).toHaveBeenCalledWith('/collection'))
    expect(session.getState()).toEqual({ status: 'signedIn', user: ash })
  })

  it('tells the API which language this device shows, for a new account', async () => {
    writePendingSignIn({ state: 'abc', verifier: 'v'.repeat(64), returnTo: '/' })
    window.history.replaceState(null, '', '/auth/callback?code=c&state=abc')
    const bodies: unknown[] = []
    vi.stubGlobal('fetch', (_url: string, init: RequestInit) => {
      bodies.push(JSON.parse(init.body as string))
      return Promise.resolve(Response.json(sessionFor()))
    })
    const onDone = vi.fn()
    const languages = new LanguageStore(new MemoryStorage(), ['pt-PT'])

    renderWithAuth(<AuthCallback onDone={onDone} />, { pathname: '/auth/callback', languages })

    expect(screen.getByRole('status')).toHaveTextContent('A iniciar sessão…')
    await vi.waitFor(() => expect(onDone).toHaveBeenCalled())
    expect(bodies).toEqual([expect.objectContaining({ language: 'pt-PT' })])
  })

  it('explains an expired link and offers to try again or go back', async () => {
    window.history.replaceState(null, '', '/auth/callback?code=c&state=abc')
    const onDone = vi.fn()

    renderWithAuth(
      <>
        <AuthCallback onDone={onDone} />
        <SignInDialog />
      </>,
      { pathname: '/auth/callback' },
    )

    expect(await screen.findByRole('alert')).toHaveTextContent('This sign-in link has expired.')
    await userEvent.click(screen.getByRole('button', { name: 'Back to the app' }))
    expect(onDone).toHaveBeenCalledWith('/')
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })

  it('explains Google refusing the code', async () => {
    writePendingSignIn({ state: 'abc', verifier: 'v'.repeat(64), returnTo: '/' })
    window.history.replaceState(null, '', '/auth/callback?code=c&state=abc')
    vi.stubGlobal('fetch', routeFetch({ '/auth/google': () => Response.json({}, { status: 400 }) }))

    renderWithAuth(<AuthCallback onDone={() => {}} />, { pathname: '/auth/callback' })

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Google didn’t confirm the sign-in. Please try again.',
    )
  })
})
