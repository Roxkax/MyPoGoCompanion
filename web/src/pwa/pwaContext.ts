import { createContext, useCallback, useContext, useSyncExternalStore } from 'react'
import type { ConnectionState, ConnectionWatch } from './connection'
import type { InstallKind, InstallOutcome, InstallWatch } from './install'
import type { InstallHintStore } from './installHint'

/** The installable app's parts (E01-F06), made once in main.tsx and handed to the screens. */
export interface Pwa {
  install: InstallWatch
  hint: InstallHintStore
  connection: ConnectionWatch
}

export const PwaContext = createContext<Pwa | null>(null)

export function usePwa(): Pwa {
  const pwa = useContext(PwaContext)
  if (!pwa) {
    throw new Error('usePwa needs a <PwaProvider> above it')
  }
  return pwa
}

/** How this browser can install the app, following changes (an announcement arrives, the app gets installed). */
export function useInstall(): { kind: InstallKind; prompt: () => Promise<InstallOutcome> } {
  const { install } = usePwa()
  const kind = useSyncExternalStore(install.subscribe, install.getKind)
  const prompt = useCallback(() => install.prompt(), [install])
  return { kind, prompt }
}

/** Whether the device has a connection, and Try again. */
export function useConnection(): ConnectionState & { tryAgain: () => Promise<void> } {
  const { connection } = usePwa()
  const state = useSyncExternalStore(connection.subscribe, connection.getState)
  const tryAgain = useCallback(() => connection.tryAgain(), [connection])
  return { ...state, tryAgain }
}

/** The one-time install hint: whether it shows now, and how to hide it for good. */
export function useInstallHint(kind: InstallKind): { shows: boolean; dismiss: () => void } {
  const { hint } = usePwa()
  useSyncExternalStore(hint.subscribe, hint.isDismissed)
  const dismiss = useCallback(() => hint.dismiss(), [hint])
  return { shows: hint.shows(kind), dismiss }
}
