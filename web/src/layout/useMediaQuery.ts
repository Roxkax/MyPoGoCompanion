import { useCallback, useSyncExternalStore } from 'react'

/** The width from which the desktop layout (sidebar and top bar) replaces the phone layout (tab bar). */
export const desktopQuery = '(min-width: 900px)'

/** Whether a CSS media query matches right now, following changes (window resized, phone rotated). */
export function useMediaQuery(query: string): boolean {
  const subscribe = useCallback(
    (onChange: () => void) => {
      const list = window.matchMedia?.(query)
      list?.addEventListener('change', onChange)
      return () => list?.removeEventListener('change', onChange)
    },
    [query],
  )
  return useSyncExternalStore(subscribe, () => window.matchMedia?.(query).matches ?? false)
}

export function useIsDesktop(): boolean {
  return useMediaQuery(desktopQuery)
}
