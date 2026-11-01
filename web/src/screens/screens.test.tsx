import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { readEnvironment } from '../environment'
import { renderApp } from '../testing/app'
import { MemoryStorage } from '../testing/auth'
import { ThemeStore, themeKey } from '../theme/theme'
import { RouteError } from './RouteError'
import { todayLabel } from './today'

beforeEach(() => vi.stubGlobal('fetch', () => new Promise(() => {})))
afterEach(() => vi.unstubAllGlobals())

describe('Settings', () => {
  it('Theme: Follow device · Light · Dark, saved on this device and applied at once', async () => {
    const storage = new MemoryStorage()
    renderApp('/settings', { themes: new ThemeStore(storage) })

    const theme = screen.getByRole('radiogroup', { name: 'Theme' })
    expect(theme).toHaveTextContent('Follow deviceLightDark')
    expect(screen.getByRole('radio', { name: 'Follow device' })).toBeChecked()

    await userEvent.click(screen.getByRole('radio', { name: 'Dark' }))

    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(storage.getItem(themeKey)).toBe('dark')

    await userEvent.click(screen.getByRole('radio', { name: 'Follow device' }))
    expect(document.documentElement.dataset.theme).toBeUndefined()
    expect(storage.getItem(themeKey)).toBeNull()
  })

  it('opens with the saved choice; another tab changing it is followed', () => {
    const storage = new MemoryStorage()
    storage.setItem(themeKey, 'light')
    renderApp('/settings', { themes: new ThemeStore(storage), layout: 'desktop' })

    expect(screen.getByRole('radio', { name: 'Light' })).toBeChecked()
    expect(document.documentElement.dataset.theme).toBe('light')

    storage.setItem(themeKey, 'dark')
    act(() => {
      window.dispatchEvent(new StorageEvent('storage', { key: themeKey }))
    })

    expect(screen.getByRole('radio', { name: 'Dark' })).toBeChecked()
  })

  it('needs no sign-in', () => {
    renderApp('/settings')

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Settings')
    expect(screen.queryByText(/Sign in to/)).not.toBeInTheDocument()
  })
})

describe('Today', () => {
  it('says the date as the design does', () => {
    const tuesday = new Date(2026, 9, 27, 18, 30)

    expect(todayLabel(tuesday, false)).toBe('Tuesday 27 October')
    expect(todayLabel(tuesday, true)).toBe('Tuesday 27 October 2026')
  })
})

describe('the component gallery', () => {
  it('shows every component and they respond', async () => {
    renderApp('/design', { environment: readEnvironment({ VITE_APP_ENV: 'local' }) })

    expect(screen.getAllByText(/^(Normal|Fire|Water|Fairy)$/)).toHaveLength(4)
    await userEvent.click(screen.getByRole('button', { name: 'Remove Dragon' }))
    expect(screen.queryByRole('button', { name: 'Remove Dragon' })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Shiny' }))
    expect(screen.getByRole('button', { name: 'Shiny' })).toHaveAttribute('aria-pressed', 'false')
    await userEvent.click(screen.getByRole('radio', { name: 'Fast80' }))
    expect(screen.getByRole('radio', { name: 'Fast80' })).toBeChecked()
  })
})

describe('RouteError', () => {
  it('offers to reload', async () => {
    const reload = vi.fn()
    vi.stubGlobal('location', { ...window.location, reload })
    render(<RouteError />)

    expect(screen.getByRole('heading')).toHaveTextContent('Something went wrong')
    await userEvent.click(screen.getByRole('button', { name: 'Reload' }))

    expect(reload).toHaveBeenCalledOnce()
  })
})
