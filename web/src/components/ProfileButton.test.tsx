import { act, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { Role } from '../auth/authApi'
import { ash, renderWithAuth, sessionFor, testSession } from '../testing/auth'
import { ProfileButton } from './ProfileButton'
import { SignInDialog } from './SignInDialog'

function signedIn(role: Role = 'User', pictureUrl: string | null = null, api = {}) {
  const { session } = testSession({ api: api as never })
  const result = renderWithAuth(<ProfileButton />, { session })
  act(() => session.accept(sessionFor({ ...ash, role, pictureUrl })))
  return result
}

describe('ProfileButton', () => {
  it('signed out: opens the sign-in screen', async () => {
    renderWithAuth(
      <>
        <ProfileButton />
        <SignInDialog />
      </>,
    )

    await userEvent.click(await screen.findByRole('button', { name: 'Sign in' }))

    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })

  it('while loading: waits', () => {
    const { session } = testSession()
    session.start = () => new Promise(() => {})
    renderWithAuth(<ProfileButton />, { session })

    expect(screen.getByRole('button', { name: 'Sign in' })).toBeDisabled()
  })

  it('signed in: the initial, then a menu with name, email and the two sign-outs', async () => {
    signedIn()

    const button = screen.getByRole('button', { name: 'Account: Ash' })
    expect(button).toHaveTextContent('A')
    await userEvent.click(button)

    expect(button).toHaveAttribute('aria-expanded', 'true')
    const menu = screen.getByRole('menu', { name: 'Account' })
    expect(menu).toHaveTextContent('Ash')
    expect(menu).toHaveTextContent('ash@example.com')
    expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual([
      'Sign out',
      'Sign out everywhere',
    ])
    expect(menu).not.toHaveTextContent('Admin')
  })

  it('shows the Google picture when there is one', () => {
    const { container } = signedIn('User', 'https://lh3.googleusercontent.com/a/ash')

    expect(container.querySelector('.avatar img')).toHaveAttribute(
      'src',
      'https://lh3.googleusercontent.com/a/ash',
    )
  })

  it.each([
    ['Admin', 'Admin'],
    ['SuperAdmin', 'Super admin'],
  ] as const)('shows the %s badge', async (role, badge) => {
    signedIn(role)

    await userEvent.click(screen.getByRole('button', { name: 'Account: Ash' }))

    expect(screen.getByText(badge)).toBeInTheDocument()
  })

  it('Sign out signs out on this device', async () => {
    const signOut = vi.fn(async () => {})
    signedIn('User', null, { signOut })
    await userEvent.click(screen.getByRole('button', { name: 'Account: Ash' }))

    await userEvent.click(screen.getByRole('menuitem', { name: 'Sign out' }))

    expect(signOut).toHaveBeenCalledWith('refresh-2')
    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('Sign out everywhere signs out every device', async () => {
    const signOutEverywhere = vi.fn(async () => {})
    signedIn('User', null, { signOutEverywhere })
    await userEvent.click(screen.getByRole('button', { name: 'Account: Ash' }))

    await userEvent.click(screen.getByRole('menuitem', { name: 'Sign out everywhere' }))

    expect(signOutEverywhere).toHaveBeenCalledWith('access-for-refresh-2')
    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('Sign out everywhere failing says so and stays signed in', async () => {
    signedIn('User', null, {
      signOutEverywhere: async () => Promise.reject(new TypeError('offline')),
    })
    await userEvent.click(screen.getByRole('button', { name: 'Account: Ash' }))

    await userEvent.click(screen.getByRole('menuitem', { name: 'Sign out everywhere' }))

    expect(await screen.findByRole('alert')).toHaveTextContent("Couldn't reach the server.")
    expect(screen.getByRole('button', { name: 'Account: Ash' })).toBeInTheDocument()
  })

  it('the menu closes on Escape and on a click outside, not on a click inside', async () => {
    signedIn()
    const button = screen.getByRole('button', { name: 'Account: Ash' })

    await userEvent.click(button)
    await userEvent.click(screen.getByText('ash@example.com'))
    expect(screen.getByRole('menu')).toBeInTheDocument()
    await userEvent.keyboard('{Shift}')
    expect(screen.getByRole('menu')).toBeInTheDocument()
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()

    await userEvent.click(button)
    await userEvent.click(document.body)
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()

    await userEvent.click(button)
    await userEvent.click(button)
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
  })
})
