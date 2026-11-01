import { act, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useAuth } from '../auth/authContext'
import { renderWithAuth, routeFetch, sessionFor } from '../testing/auth'
import { SignInDialog } from './SignInDialog'

afterEach(() => vi.unstubAllGlobals())

function Opener() {
  const { openSignIn } = useAuth()
  return <button onClick={() => openSignIn()}>Sign in</button>
}

async function openDialog(options: Parameters<typeof renderWithAuth>[1] = {}) {
  const result = renderWithAuth(
    <>
      <Opener />
      <SignInDialog />
    </>,
    options,
  )
  await userEvent.click(screen.getByText('Sign in'))
  return result
}

describe('SignInDialog', () => {
  it('is closed until opened', () => {
    renderWithAuth(<SignInDialog />)

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('shows what signing in adds, Google, "Not now" and the privacy note', async () => {
    await openDialog()

    const dialog = screen.getByRole('dialog', { name: 'Sign in to make it yours' })
    expect(dialog).toHaveTextContent('Everything else works without an account')
    expect(screen.getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'Your collection, uploaded from Poke Genie',
      '"Your counters" and "your team" everywhere',
      'Saved filters on every device',
      'Ask the AI assistant',
    ])
    expect(screen.getByRole('button', { name: 'Continue with Google' })).toBeInTheDocument()
    expect(dialog).toHaveTextContent('No passwords')
    expect(dialog).toHaveTextContent('not affiliated with Niantic or The Pokémon Company')
    expect(dialog).toHaveFocus()
  })

  it.each(['Not now, keep browsing', '‹ Back'])('"%s" closes it', async (label) => {
    await openDialog()

    await userEvent.click(screen.getByRole('button', { name: label }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('Escape and a click outside close it; a click inside does not', async () => {
    await openDialog()
    await userEvent.click(screen.getByRole('dialog'))
    expect(screen.getByRole('dialog')).toBeInTheDocument()

    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    await userEvent.click(screen.getByText('Sign in'))
    await userEvent.keyboard('a')
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    await userEvent.click(document.querySelector('.sign-in-overlay')!)
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('Continue with Google leaves for Google', async () => {
    vi.stubGlobal(
      'fetch',
      routeFetch({ '/auth/google/client': () => Response.json({ clientId: 'cid' }) }),
    )
    const navigate = vi.fn()
    await openDialog({ navigate })

    await userEvent.click(screen.getByRole('button', { name: 'Continue with Google' }))

    expect(navigate).toHaveBeenCalledOnce()
  })

  it('says when sign-in is not set up', async () => {
    vi.stubGlobal(
      'fetch',
      routeFetch({ '/auth/google/client': () => Response.json({}, { status: 503 }) }),
    )
    await openDialog()

    await userEvent.click(screen.getByRole('button', { name: 'Continue with Google' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "Sign-in isn't set up on this server yet.",
    )
    expect(screen.getByRole('button', { name: 'Continue with Google' })).toBeEnabled()
  })

  it('closes once someone is signed in', async () => {
    const { session } = await openDialog()

    act(() => session.accept(sessionFor()))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})
