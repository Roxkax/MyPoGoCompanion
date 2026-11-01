// The one-time install hint (decision 2026-11-01): on the phone, from the second visit, where the app can really be
// installed (the browser's install window, or the iPhone guide). Closing it, or installing, hides it for good on this
// device. A visit is a browser session: opening the app again after closing it counts, reloading doesn't.

import type { InstallKind } from './install'

export const visitsKey = 'mpgc.visits'
export const visitKey = 'mpgc.visit'
export const hintKey = 'mpgc.installHint'

/** From which visit the hint shows. */
export const hintFromVisit = 2

type SimpleStorage = Pick<Storage, 'getItem' | 'setItem'>

function read(storage: SimpleStorage | null, key: string): string | null {
  try {
    return storage?.getItem(key) ?? null
  } catch {
    return null
  }
}

function write(storage: SimpleStorage | null, key: string, value: string): void {
  try {
    storage?.setItem(key, value)
  } catch {
    // Storage blocked (private browsing in some browsers): the hint just shows less.
  }
}

/** Storage the browser may refuse to hand out (a sandboxed page): `null` then. */
export function storageOrNull(get: () => Storage): Storage | null {
  try {
    return get()
  } catch {
    return null
  }
}

type Listener = () => void

export class InstallHintStore {
  private dismissed: boolean
  private readonly listeners = new Set<Listener>()
  private readonly local: SimpleStorage | null
  private readonly session: SimpleStorage | null

  constructor(
    local: SimpleStorage | null = storageOrNull(() => window.localStorage),
    session: SimpleStorage | null = storageOrNull(() => window.sessionStorage),
  ) {
    this.local = local
    this.session = session
    this.dismissed = read(local, hintKey) === 'dismissed'
  }

  /** Counts this visit once per browser session; returns the number of visits so far. */
  countVisit(): number {
    const visits = this.visits()
    if (read(this.session, visitKey)) {
      return visits
    }
    write(this.session, visitKey, '1')
    write(this.local, visitsKey, String(visits + 1))
    return visits + 1
  }

  visits(): number {
    const visits = Number.parseInt(read(this.local, visitsKey) ?? '0', 10)
    return Number.isFinite(visits) && visits > 0 ? visits : 0
  }

  readonly isDismissed = (): boolean => this.dismissed

  readonly subscribe = (listener: Listener): (() => void) => {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }

  /** Hides the hint for good on this device. */
  dismiss(): void {
    write(this.local, hintKey, 'dismissed')
    this.dismissed = true
    this.listeners.forEach((listener) => listener())
  }

  /** Whether the hint shows now. */
  shows(kind: InstallKind): boolean {
    return (
      !this.dismissed && (kind === 'prompt' || kind === 'ios') && this.visits() >= hintFromVisit
    )
  }
}
