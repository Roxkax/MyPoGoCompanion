// Installing the app (Settings → Install the app and the one-time hint, E01-F06). Browsers differ:
// - Chrome, Edge and Android's browsers announce they can install it (`beforeinstallprompt`): the app keeps that
//   announcement and opens the browser's own install window when asked ("prompt").
// - iPhone and iPad have no such window: a short guide shows Share → Add to Home Screen ("ios").
// - Elsewhere the browser's menu may have it (Safari on a Mac: File → Add to Dock; Firefox on a computer can't):
//   a guide says where to look ("menu").
// - Opened as the installed app, there is nothing to offer ("installed").

/** Chrome's announcement that the app can be installed; `prompt()` opens its install window. */
export interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>
  readonly userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

export type InstallKind = 'installed' | 'prompt' | 'ios' | 'menu'

export type InstallOutcome = 'accepted' | 'dismissed' | 'unavailable'

export interface InstallEnvironment {
  addEventListener(type: string, listener: (event: Event) => void): void
  removeEventListener(type: string, listener: (event: Event) => void): void
  matchMedia?(query: string): Pick<MediaQueryList, 'matches'>
  navigator: { userAgent: string; maxTouchPoints?: number; standalone?: boolean }
}

/** Opened from the home screen, not in a browser tab. */
export const standaloneQuery = '(display-mode: standalone)'

/** iPhone, iPad (which says "Macintosh" but has a touch screen) and iPod. */
export function isAppleMobile(navigator: InstallEnvironment['navigator']): boolean {
  return (
    /iPhone|iPad|iPod/.test(navigator.userAgent) ||
    (/Macintosh/.test(navigator.userAgent) && (navigator.maxTouchPoints ?? 0) > 1)
  )
}

type Listener = () => void

export class InstallWatch {
  private deferred: BeforeInstallPromptEvent | null = null
  private installed: boolean
  private kind: InstallKind
  private readonly listeners = new Set<Listener>()
  private readonly environment: InstallEnvironment

  constructor(environment: InstallEnvironment = window as unknown as InstallEnvironment) {
    this.environment = environment
    this.installed =
      environment.matchMedia?.(standaloneQuery).matches === true ||
      environment.navigator.standalone === true
    this.kind = this.work()
  }

  readonly getKind = (): InstallKind => this.kind

  readonly subscribe = (listener: Listener): (() => void) => {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }

  /** Listens for the browser's announcements. Started before the app draws, as Chrome may announce early. */
  start(): () => void {
    const onPrompt = (event: Event) => {
      // Keep it for our own button and hint instead of Chrome's own banner.
      event.preventDefault()
      this.deferred = event as BeforeInstallPromptEvent
      this.changed()
    }
    const onInstalled = () => {
      this.installed = true
      this.deferred = null
      this.changed()
    }
    this.environment.addEventListener('beforeinstallprompt', onPrompt)
    this.environment.addEventListener('appinstalled', onInstalled)
    return () => {
      this.environment.removeEventListener('beforeinstallprompt', onPrompt)
      this.environment.removeEventListener('appinstalled', onInstalled)
    }
  }

  /** Opens the browser's install window. The announcement works once; Chrome sends a new one if it's refused. */
  async prompt(): Promise<InstallOutcome> {
    const deferred = this.deferred
    if (!deferred) {
      return 'unavailable'
    }
    this.deferred = null
    this.changed()
    await deferred.prompt()
    const { outcome } = await deferred.userChoice
    if (outcome === 'accepted') {
      this.installed = true
      this.changed()
    }
    return outcome
  }

  private work(): InstallKind {
    if (this.installed) {
      return 'installed'
    }
    if (this.deferred) {
      return 'prompt'
    }
    return isAppleMobile(this.environment.navigator) ? 'ios' : 'menu'
  }

  private changed(): void {
    this.kind = this.work()
    this.listeners.forEach((listener) => listener())
  }
}
