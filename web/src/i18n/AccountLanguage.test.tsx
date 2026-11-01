import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { renderApp } from '../testing/app'
import { ash, MemoryStorage, routeFetch, sessionFor } from '../testing/auth'
import { LanguageStore } from './language'
import { useLanguage } from './languageContext'
import { languageKey } from './languages'
import { readRequest } from '../testing/fetch'

function device(saved?: 'en' | 'pt-PT', browser: readonly string[] = ['en-GB']) {
  const storage = new MemoryStorage()
  if (saved) {
    storage.setItem(languageKey, saved)
  }
  return { storage, languages: new LanguageStore(storage, browser) }
}

beforeEach(() => vi.stubGlobal('fetch', () => new Promise(() => {})))
afterEach(() => vi.unstubAllGlobals())

describe('Settings → Language', () => {
  it('English and Português (Portugal), each in its own language; the choice applies at once', async () => {
    const { storage, languages } = device()
    renderApp('/settings', { languages })

    const group = screen.getByRole('radiogroup', { name: 'Language' })
    expect(group).toHaveTextContent('English✓Português (Portugal)')
    expect(screen.getByRole('radio', { name: 'English' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Português (Portugal)' })).toHaveAttribute(
      'lang',
      'pt-PT',
    )
    expect(screen.getByText(/Search strings stay in English/)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('radio', { name: 'Português (Portugal)' }))

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Definições')
    expect(screen.getByRole('radiogroup', { name: 'Idioma' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Seguir o dispositivo' })).toBeChecked()
    expect(document.documentElement.lang).toBe('pt-PT')
    expect(document.title).toBe('Definições · MyPoGoCompanion')
    expect(storage.getItem(languageKey)).toBe('pt-PT')
  })

  it('the arrow keys move the choice', async () => {
    const { languages } = device()
    renderApp('/settings', { languages })

    screen.getByRole('radio', { name: 'English' }).focus()
    await userEvent.keyboard('{ArrowDown}')
    expect(screen.getByRole('radio', { name: 'Português (Portugal)' })).toHaveFocus()
    expect(languages.getLanguage()).toBe('pt-PT')

    await userEvent.keyboard('{ArrowDown}')
    expect(languages.getLanguage()).toBe('en')
    await userEvent.keyboard('{ArrowUp}{Enter}')
    expect(languages.getLanguage()).toBe('pt-PT')
  })

  it('signed out, nothing goes to the server', async () => {
    const fetch = vi.fn<typeof globalThis.fetch>(() => new Promise<Response>(() => {}))
    vi.stubGlobal('fetch', fetch)
    const { languages } = device()
    renderApp('/settings', { languages })

    await userEvent.click(screen.getByRole('radio', { name: 'Português (Portugal)' }))

    expect(
      fetch.mock.calls.some(([input]) =>
        (input instanceof Request ? input.url : String(input)).includes('/me/language'),
      ),
    ).toBe(false)
  })

  it('signed in, the choice is also saved with the account', async () => {
    const saved: unknown[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const sent = await readRequest(input, init)
        if (sent.url.endsWith('/me/language')) {
          saved.push({
            method: sent.method,
            auth: sent.headers.get('Authorization'),
            body: sent.body,
          })
          return new Response(null, { status: 204 })
        }
        return new Promise<Response>(() => {})
      }),
    )
    const { languages } = device()
    const { session } = renderApp('/settings', { languages, user: ash })

    await userEvent.click(screen.getByRole('radio', { name: 'Português (Portugal)' }))

    await vi.waitFor(() =>
      expect(saved).toEqual([
        { method: 'PUT', auth: 'Bearer access-for-refresh-2', body: { language: 'pt-PT' } },
      ]),
    )
    const state = session.getState()
    expect(state.status === 'signedIn' && state.user.language).toBe('pt-PT')
    expect(languages.getLanguage()).toBe('pt-PT')
  })

  it('signed in but the account can’t be reached: this device keeps it and says so', async () => {
    vi.stubGlobal(
      'fetch',
      routeFetch({ '/me/language': () => Promise.reject(new TypeError('offline')) }),
    )
    const { languages } = device()
    renderApp('/settings', { languages, user: ash })

    await userEvent.click(screen.getByRole('radio', { name: 'Português (Portugal)' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Mudou neste dispositivo, mas não foi possível guardá-lo na tua conta.',
    )
    expect(languages.getLanguage()).toBe('pt-PT')
  })
})

describe('the account’s language wins (decision 2026-11-01)', () => {
  it('signing in switches this device to the account’s language and keeps it', () => {
    const { storage, languages } = device('en')
    renderApp('/', { languages, user: { ...ash, language: 'pt-PT' } })

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Hoje')
    expect(screen.getByRole('navigation', { name: 'Separadores' })).toHaveTextContent('Coleção')
    expect(storage.getItem(languageKey)).toBe('pt-PT')
  })

  it('each refresh brings the account’s current language (changed on another device)', () => {
    const { languages } = device()
    const { session } = renderApp('/', { languages, user: ash })
    expect(languages.getLanguage()).toBe('en')

    act(() => session.accept(sessionFor({ ...ash, language: 'pt-PT' }, 'refresh-3')))

    expect(languages.getLanguage()).toBe('pt-PT')
  })

  it('signing out keeps the language the device was showing', async () => {
    const { languages } = device()
    const { session } = renderApp('/', { languages, user: { ...ash, language: 'pt-PT' } })

    await act(() => session.signOut())

    expect(languages.getLanguage()).toBe('pt-PT')
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Hoje')
  })

  it('an unknown language from the server changes nothing', () => {
    const { languages } = device('pt-PT')
    renderApp('/', { languages, user: { ...ash, language: 'xx' } })

    expect(languages.getLanguage()).toBe('pt-PT')
  })

  it('the language hooks need their provider', () => {
    function Probe() {
      useLanguage()
      return null
    }
    vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() => render(<Probe />)).toThrow('useLanguage must be used inside <LanguageProvider>')
  })

  it('the first visit follows the browser: Portuguese gives Português (Portugal)', () => {
    const { languages } = device(undefined, ['pt-BR'])
    renderApp('/', { languages })

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Hoje')
  })
})
