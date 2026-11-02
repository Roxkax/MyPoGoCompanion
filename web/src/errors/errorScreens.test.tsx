import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api/client'
import { ErrorReference } from '../components/ErrorReference'
import { LoadError } from '../components/LoadError'
import { readEnvironment } from '../environment'
import { ConnectionWatch } from '../pwa/connection'
import { currentAddress, renderApp } from '../testing/app'

const qa = readEnvironment({ VITE_APP_ENV: 'qa' })

beforeEach(() => vi.stubGlobal('fetch', () => new Promise(() => {})))
afterEach(() => vi.unstubAllGlobals())

/** React logs the error a screen throws; the tests expect it, so it stays out of the output. */
function quietErrors() {
  const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
  return () => spy.mockRestore()
}

describe('a screen that breaks', () => {
  it('shows "Something went wrong" inside the frame, reports it once and shows its reference', async () => {
    const restore = quietErrors()
    const report = vi.fn((_kind: string, _error: unknown, reference?: string) => reference ?? '')
    renderApp('/pokedex?fail=screen', { environment: qa, reporter: { report } })

    expect(await screen.findByRole('heading', { name: 'Something went wrong' })).toBeVisible()
    expect(screen.getByRole('navigation', { name: 'Tabs' })).toBeVisible()
    const reference = report.mock.calls[0]?.[2]
    expect(reference).toMatch(/^[0-9a-f]{8}$/)
    expect(report.mock.calls.every(([kind, , ref]) => kind === 'Screen' && ref === reference)).toBe(
      true,
    )
    expect(String(report.mock.calls[0]?.[1])).toContain('?fail=screen')
    expect(screen.getByText(reference!)).toBeVisible()
    restore()
  })

  it('Reload reloads the app', async () => {
    const restore = quietErrors()
    renderApp('/pokedex?fail=screen', { environment: qa })
    const reloadButton = await screen.findByRole('button', { name: 'Reload' })
    const reload = vi.fn()
    vi.stubGlobal('location', { ...window.location, reload })
    await userEvent.click(reloadButton)
    expect(reload).toHaveBeenCalledOnce()
    restore()
  })

  it('Go to Today leaves the broken screen', async () => {
    const restore = quietErrors()
    renderApp('/pokedex?fail=screen', { environment: qa })
    await userEvent.click(await screen.findByRole('link', { name: 'Go to Today' }))
    expect(currentAddress()).toBe('/')
    expect(screen.queryByRole('heading', { name: 'Something went wrong' })).not.toBeInTheDocument()
    restore()
  })

  it('never breaks on purpose in production', () => {
    renderApp('/settings?fail=screen')
    expect(screen.queryByRole('heading', { name: 'Something went wrong' })).not.toBeInTheDocument()
  })

  it('?fail=script throws an error nobody catches', () => {
    vi.useFakeTimers()
    try {
      renderApp('/settings?fail=script', { environment: qa })
      expect(() => vi.runOnlyPendingTimers()).toThrow(
        'Test error made on purpose with ?fail=script',
      )
    } finally {
      vi.useRealTimers()
    }
  })
})

describe('the API not answering', () => {
  const watch = () =>
    new ConnectionWatch({
      check: () => Promise.resolve(true),
      checkApi: () => Promise.resolve(false),
      reload: () => {},
    })

  it('replaces the screen with "isn’t answering", Try again and the reference, inside the frame', async () => {
    const connection = watch()
    renderApp('/pokedex', { pwa: { connection } })

    act(() => connection.pretendApiDown('4bf92f35'))

    expect(
      screen.getByRole('heading', { name: 'MyPoGoCompanion isn’t answering right now' }),
    ).toBeVisible()
    expect(screen.getByText('4bf92f35')).toBeVisible()
    expect(screen.getByRole('navigation', { name: 'Tabs' })).toBeVisible()

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByText(/Still no answer/)).toBeVisible()
  })

  it('shows no reference when the API gave none', () => {
    const connection = watch()
    renderApp('/', { pwa: { connection } })
    act(() => connection.pretendApiDown())
    expect(screen.queryByText('Reference')).not.toBeInTheDocument()
  })

  it('?fail=api shows it on QA and local copies', () => {
    renderApp('/?fail=api', { environment: qa, pwa: { connection: watch() } })
    expect(
      screen.getByRole('heading', { name: 'MyPoGoCompanion isn’t answering right now' }),
    ).toBeVisible()
  })

  it('is in Portuguese too', async () => {
    const connection = watch()
    const { languages } = renderApp('/', { pwa: { connection } })
    act(() => languages.setLanguage('pt-PT'))
    act(() => connection.pretendApiDown('4bf92f35'))
    expect(
      await screen.findByRole('heading', { name: 'O MyPoGoCompanion não está a responder' }),
    ).toBeVisible()
    expect(screen.getByText('Referência')).toBeVisible()
  })
})

describe('LoadError and ErrorReference', () => {
  it('says the data couldn’t be loaded, offers Try again and shows the API’s reference', async () => {
    const retry = vi.fn()
    render(<LoadError error={new ApiError(500, 'Boom', '4bf92f35')} onRetry={retry} />)

    expect(screen.getByRole('alert')).toHaveTextContent('This couldn’t be loaded')
    expect(screen.getByText('4bf92f35')).toBeVisible()
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
    expect(retry).toHaveBeenCalledOnce()
  })

  it('shows no reference for other errors', () => {
    render(<LoadError error={new Error('x')} onRetry={() => {}} />)
    expect(screen.queryByText('Reference')).not.toBeInTheDocument()
  })

  it('shows nothing without a reference', () => {
    const { container } = render(<ErrorReference reference={null} />)
    expect(container).toBeEmptyDOMElement()
  })
})
