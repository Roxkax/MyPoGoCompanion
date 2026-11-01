import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { readEnvironment } from '../environment'
import { LanguageStore } from '../i18n/language'
import { renderApp } from '../testing/app'
import { MemoryStorage } from '../testing/auth'
import { ConnectionWatch, type ConnectionEnvironment } from './connection'
import { InstallWatch, type BeforeInstallPromptEvent, type InstallEnvironment } from './install'
import { InstallHintStore, visitsKey } from './installHint'
import { usePwa } from './pwaContext'

beforeEach(() => vi.stubGlobal('fetch', () => new Promise(() => {})))
afterEach(() => vi.unstubAllGlobals())

const iPhone =
  'Mozilla/5.0 (iPhone; CPU iPhone OS 19_0 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148'

/**
 * A browser that can install the app in the given way: `prompt` (Chrome announced it; the install window answers
 * `outcome`), `ios`, `menu` or `installed`.
 */
function installAs(
  kind: 'prompt' | 'ios' | 'menu' | 'installed',
  outcome: 'accepted' | 'dismissed' = 'dismissed',
) {
  const listeners = new Map<string, (event: Event) => void>()
  const install = new InstallWatch({
    addEventListener: (type, listener) => listeners.set(type, listener),
    removeEventListener: () => {},
    matchMedia: () => ({ matches: kind === 'installed' }),
    navigator: { userAgent: kind === 'ios' ? iPhone : 'Chrome' },
  } satisfies InstallEnvironment)
  install.start()
  const prompt = vi.fn(() => Promise.resolve())
  if (kind === 'prompt') {
    const event = new Event('beforeinstallprompt', { cancelable: true })
    Object.assign(event, { prompt, userChoice: Promise.resolve({ outcome }) })
    listeners.get('beforeinstallprompt')!(event as BeforeInstallPromptEvent)
  }
  return { install, prompt }
}

/** The hint's storage on the given visit. */
function visit(number: number) {
  const local = new MemoryStorage()
  local.setItem(visitsKey, String(number))
  return { hint: new InstallHintStore(local, new MemoryStorage()), local }
}

/** A connection that starts online or offline; `goOffline()` is the browser losing it. */
function connection(options: { onLine?: boolean; connected?: boolean } = {}) {
  const listeners = new Map<string, () => void>()
  let connected = options.connected ?? false
  const check = vi.fn(async () => connected)
  const reload = vi.fn()
  const watch = new ConnectionWatch({
    environment: {
      navigator: { onLine: options.onLine ?? true },
      addEventListener: (type: string, listener: () => void) => listeners.set(type, listener),
      removeEventListener: () => {},
      document: {
        visibilityState: 'visible',
        addEventListener: () => {},
        removeEventListener: () => {},
      },
    } as unknown as ConnectionEnvironment,
    check,
    reload,
  })
  const stop = watch.start()
  return {
    connection: watch,
    check,
    reload,
    stop,
    goOffline: () => act(() => listeners.get('offline')!()),
    connect: () => (connected = true),
  }
}

describe('the offline screen (H3)', () => {
  it('takes the screen’s place on the phone, keeping the tab bar', () => {
    const { connection: watch, stop } = connection({ onLine: false })
    renderApp('/pokedex', { pwa: { connection: watch } })

    expect(screen.getByRole('heading', { level: 1, name: 'You’re offline' })).toBeInTheDocument()
    expect(screen.getByText(/needs a connection for now/)).toBeInTheDocument()
    expect(screen.getByText(/planned for a later version/)).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Tabs' })).toBeInTheDocument()
    expect(screen.queryByText('Coming soon')).not.toBeInTheDocument()
    stop()
  })

  it('takes the screen’s place on the desktop, keeping the sidebar and top bar', () => {
    const { connection: watch, stop } = connection({ onLine: false })
    renderApp('/', { pwa: { connection: watch }, layout: 'desktop' })

    expect(screen.getByRole('heading', { name: 'You’re offline' })).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument()
    stop()
  })

  it('appears as soon as the connection goes, wherever you are', () => {
    const { connection: watch, goOffline, stop } = connection()
    renderApp('/battle/raids', { pwa: { connection: watch } })
    expect(screen.queryByText('You’re offline')).not.toBeInTheDocument()

    goOffline()
    expect(screen.getByRole('heading', { name: 'You’re offline' })).toBeInTheDocument()
    stop()
  })

  it('Try again says when there is still no connection', async () => {
    const { connection: watch, check, reload, stop } = connection({ onLine: false })
    renderApp('/', { pwa: { connection: watch } })

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
    expect(check).toHaveBeenCalled()
    expect(await screen.findByRole('status')).toHaveTextContent('Still no connection')
    expect(reload).not.toHaveBeenCalled()
    stop()
  })

  it('Try again reloads the app when the connection is back', async () => {
    const { connection: watch, connect, reload, stop } = connection({ onLine: false })
    renderApp('/', { pwa: { connection: watch } })
    connect()

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
    expect(reload).toHaveBeenCalledTimes(1)
    expect(screen.getByRole('button', { name: 'Checking…' })).toBeDisabled()
    stop()
  })

  it('speaks Portuguese', () => {
    const { connection: watch, stop } = connection({ onLine: false })
    renderApp('/', {
      pwa: { connection: watch },
      languages: new LanguageStore(new MemoryStorage(), ['pt-PT']),
    })
    expect(screen.getByRole('heading', { name: 'Estás sem ligação' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
    stop()
  })
})

describe('Settings → Install the app', () => {
  it('on Chrome and Android opens the browser’s own install window', async () => {
    const { install, prompt } = installAs('prompt')
    renderApp('/settings', { pwa: { install } })

    const more = screen.getByRole('region', { name: 'More' })
    const row = within(more).getByRole('button', { name: /Install the app/ })
    expect(row).toHaveTextContent('Add to home screen')
    await userEvent.click(row)
    expect(prompt).toHaveBeenCalledTimes(1)
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('on an iPhone shows the steps: Share, Add to Home Screen, Add', async () => {
    const { install } = installAs('ios')
    renderApp('/settings', { pwa: { install } })

    await userEvent.click(screen.getByRole('button', { name: /Install the app/ }))
    const guide = screen.getByRole('dialog', { name: 'Install MyPoGoCompanion' })
    expect(within(guide).getAllByRole('listitem')).toHaveLength(3)
    expect(guide).toHaveTextContent('Share button')
    expect(guide).toHaveTextContent('Add to Home Screen')

    await userEvent.click(within(guide).getByRole('button', { name: 'Got it' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('elsewhere says where the browser keeps it (desktop wording)', async () => {
    const { install } = installAs('menu')
    renderApp('/settings', { pwa: { install }, layout: 'desktop' })

    const row = screen.getByRole('button', { name: /Install the app/ })
    expect(row).toHaveTextContent('Add to your computer')
    await userEvent.click(row)
    const guide = screen.getByRole('dialog')
    expect(guide).toHaveTextContent('Install app')
    expect(guide).toHaveTextContent('File → Add to Dock')
    expect(guide).toHaveTextContent('Firefox')

    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('closes the guide with ✕ or by tapping outside it', async () => {
    const { install } = installAs('menu')
    renderApp('/settings', { pwa: { install } })

    await userEvent.click(screen.getByRole('button', { name: /Install the app/ }))
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Close' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /Install the app/ }))
    await userEvent.click(screen.getByRole('dialog').parentElement!)
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('is hidden once the app is installed', () => {
    const { install } = installAs('installed')
    renderApp('/settings', { pwa: { install } })
    expect(screen.queryByRole('region', { name: 'More' })).not.toBeInTheDocument()
    expect(screen.queryByText('Install the app')).not.toBeInTheDocument()
  })
})

describe('the one-time install hint', () => {
  it('shows on the phone from the second visit where the browser can install', () => {
    const { install } = installAs('prompt')
    renderApp('/', { pwa: { install, hint: visit(2).hint } })
    const hint = screen.getByRole('complementary', { name: 'Install the app' })
    expect(hint).toHaveTextContent('Add MyPoGoCompanion to your home screen.')
  })

  it('doesn’t show on the first visit', () => {
    const { install } = installAs('prompt')
    renderApp('/', { pwa: { install, hint: visit(1).hint } })
    expect(screen.queryByRole('complementary', { name: 'Install the app' })).not.toBeInTheDocument()
  })

  it('doesn’t show where the browser keeps install in its menu, nor once installed', () => {
    renderApp('/', { pwa: { install: installAs('menu').install, hint: visit(3).hint } })
    expect(screen.queryByRole('complementary', { name: 'Install the app' })).not.toBeInTheDocument()
  })

  it('doesn’t show on the desktop, on screens without the tab bar, or offline', () => {
    const offline = connection({ onLine: false })
    const { unmount } = renderApp('/', {
      pwa: { install: installAs('prompt').install, hint: visit(2).hint },
      layout: 'desktop',
    })
    expect(screen.queryByRole('complementary', { name: 'Install the app' })).not.toBeInTheDocument()
    unmount()
    const settings = renderApp('/settings', {
      pwa: { install: installAs('prompt').install, hint: visit(2).hint },
    })
    expect(screen.queryByRole('complementary', { name: 'Install the app' })).not.toBeInTheDocument()
    settings.unmount()
    renderApp('/', {
      pwa: {
        install: installAs('prompt').install,
        hint: visit(2).hint,
        connection: offline.connection,
      },
    })
    expect(screen.queryByRole('complementary', { name: 'Install the app' })).not.toBeInTheDocument()
    offline.stop()
  })

  it('Install opens the browser’s install window and the hint goes for good', async () => {
    const { install, prompt } = installAs('prompt')
    const { hint, local } = visit(2)
    renderApp('/', { pwa: { install, hint } })

    await userEvent.click(screen.getByRole('button', { name: 'Install' }))
    expect(prompt).toHaveBeenCalledTimes(1)
    expect(screen.queryByRole('complementary', { name: 'Install the app' })).not.toBeInTheDocument()
    expect(local.getItem('mpgc.installHint')).toBe('dismissed')
  })

  it('on an iPhone, Install shows the steps', async () => {
    const { install } = installAs('ios')
    renderApp('/', { pwa: { install, hint: visit(2).hint } })

    await userEvent.click(screen.getByRole('button', { name: 'Install' }))
    expect(screen.getByRole('dialog', { name: 'Install MyPoGoCompanion' })).toBeInTheDocument()
    expect(screen.queryByRole('complementary', { name: 'Install the app' })).not.toBeInTheDocument()
  })

  it('✕ hides it for good on this device', async () => {
    const { install } = installAs('prompt')
    const { hint, local } = visit(4)
    renderApp('/', { pwa: { install, hint } })

    await userEvent.click(screen.getByRole('button', { name: 'Don’t show this again' }))
    expect(screen.queryByRole('complementary', { name: 'Install the app' })).not.toBeInTheDocument()
    expect(new InstallHintStore(local, new MemoryStorage()).shows('prompt')).toBe(false)
  })

  it('uses this copy’s icon (orange on QA)', () => {
    const { install } = installAs('prompt')
    const { container } = renderApp('/', {
      pwa: { install, hint: visit(2).hint },
      environment: readEnvironment({ VITE_APP_ENV: 'qa' }),
    })
    expect(container.querySelector('.install-hint img')).toHaveAttribute('src', '/icon-qa.svg')
  })
})

describe('usePwa', () => {
  it('needs a PwaProvider', () => {
    function Lonely() {
      usePwa()
      return null
    }
    vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() => render(<Lonely />)).toThrow('PwaProvider')
  })
})
