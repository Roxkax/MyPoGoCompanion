import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { MemoryStorage, renderWithAuth, routeFetch, sessionFor, testSession } from '../testing/auth'
import { currentPath, useAuth } from './authContext'
import { refreshTokenKey } from './storage'

afterEach(() => vi.unstubAllGlobals())

function Probe() {
  const { state, dialog, openSignIn, closeSignIn, continueWithGoogle, signInError } = useAuth()
  return (
    <div>
      <p>status: {state.status}</p>
      <p>
        dialog: {String(dialog.open)} {dialog.returnTo}
      </p>
      <p>error: {signInError ?? 'none'}</p>
      <button onClick={() => openSignIn()}>open</button>
      <button onClick={() => openSignIn('/eggs')}>open for eggs</button>
      <button onClick={closeSignIn}>close</button>
      <button onClick={() => void continueWithGoogle('/raids')}>google</button>
      <button onClick={() => void continueWithGoogle()}>google here</button>
    </div>
  )
}

describe('AuthProvider', () => {
  it('starts the session on normal pages', async () => {
    renderWithAuth(<Probe />)

    expect(await screen.findByText('status: signedOut')).toBeInTheDocument()
  })

  it('leaves the callback page to finish the sign-in itself', async () => {
    const storage = new MemoryStorage()
    storage.setItem(refreshTokenKey, 'r')
    const refreshSession = vi.fn()
    const { session } = testSession({ storage, api: { refreshSession } as never })

    renderWithAuth(<Probe />, { session, pathname: '/auth/callback' })

    expect(screen.getByText('status: loading')).toBeInTheDocument()
    expect(refreshSession).not.toHaveBeenCalled()
  })

  it('opens the sign-in screen remembering the current page, and closes it', async () => {
    window.history.replaceState(null, '', '/pokedex?q=char#top')
    renderWithAuth(<Probe />)

    await userEvent.click(screen.getByText('open'))
    expect(screen.getByText('dialog: true /pokedex?q=char#top')).toBeInTheDocument()
    await userEvent.click(screen.getByText('close'))
    expect(screen.getByText('dialog: false /pokedex?q=char#top')).toBeInTheDocument()
    window.history.replaceState(null, '', '/')
  })

  it('leaves for Google with the page to come back to', async () => {
    vi.stubGlobal(
      'fetch',
      routeFetch({ '/auth/google/client': () => Response.json({ clientId: 'cid' }) }),
    )
    const navigate = vi.fn()
    renderWithAuth(<Probe />, { navigate })

    await userEvent.click(screen.getByText('google'))

    expect(navigate).toHaveBeenCalledWith(
      expect.stringMatching(/^https:\/\/accounts\.google\.com\//),
    )
    expect(JSON.parse(sessionStorage.getItem('mpgc.pendingSignIn')!).returnTo).toBe('/raids')
    await userEvent.click(screen.getByText('google here'))
    expect(JSON.parse(sessionStorage.getItem('mpgc.pendingSignIn')!).returnTo).toBe('/')
  })

  it.each([
    [503, "Sign-in isn't set up on this server yet."],
    [500, "Couldn't reach the app's server. Check your connection and try again."],
  ])('explains a failed start (%s)', async (status, message) => {
    vi.stubGlobal(
      'fetch',
      routeFetch({ '/auth/google/client': () => Response.json({}, { status }) }),
    )
    const navigate = vi.fn()
    renderWithAuth(<Probe />, { navigate })

    await userEvent.click(screen.getByText('google'))

    expect(await screen.findByText(`error: ${message}`)).toBeInTheDocument()
    expect(navigate).not.toHaveBeenCalled()
  })

  it('follows another tab signing out', async () => {
    const { session, storage } = testSession()
    renderWithAuth(<Probe />, { session })
    act(() => session.accept(sessionFor()))
    expect(screen.getByText('status: signedIn')).toBeInTheDocument()

    storage.removeItem(refreshTokenKey)
    act(() => {
      window.dispatchEvent(new StorageEvent('storage', { key: refreshTokenKey, newValue: null }))
    })

    expect(screen.getByText('status: signedOut')).toBeInTheDocument()
  })

  it('creates its own session and navigation by default', async () => {
    localStorage.clear()
    const { AuthProvider } = await import('./AuthProvider')

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )

    expect(await screen.findByText('status: signedOut')).toBeInTheDocument()
  })

  it('useAuth outside the provider explains the mistake', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() => render(<Probe />)).toThrow('useAuth must be used inside <AuthProvider>')
  })

  it('currentPath joins path, query and fragment', () => {
    expect(currentPath({ pathname: '/a', search: '?b=1', hash: '#c' })).toBe('/a?b=1#c')
  })
})
