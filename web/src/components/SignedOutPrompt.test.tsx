import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { renderWithAuth, routeFetch } from '../testing/auth'
import { SignedOutPrompt } from './SignedOutPrompt'

afterEach(() => vi.unstubAllGlobals())

describe('SignedOutPrompt', () => {
  it.each([
    ['collection', 'Your collection lives here', 'upload your Poke Genie export'],
    ['savedFilters', 'Keep your filters', 'save the search strings'],
    ['ai', 'Ask the AI assistant', 'answered from the app’s own data'],
  ] as const)('%s: a title, why to sign in and Continue with Google', (feature, title, text) => {
    renderWithAuth(<SignedOutPrompt feature={feature} />)

    expect(screen.getByRole('region', { name: title })).toHaveTextContent(text)
    expect(screen.getByRole('button', { name: 'Continue with Google' })).toBeInTheDocument()
  })

  it('goes straight to Google and shows a failure', async () => {
    vi.stubGlobal(
      'fetch',
      routeFetch({ '/auth/google/client': () => Promise.reject(new TypeError('offline')) }),
    )
    renderWithAuth(<SignedOutPrompt feature="collection" />)

    await userEvent.click(screen.getByRole('button', { name: 'Continue with Google' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Couldn’t reach the app’s server.')
  })
})
