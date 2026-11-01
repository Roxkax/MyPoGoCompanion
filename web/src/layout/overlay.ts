import { useCallback } from 'react'
import { useLocation, useNavigate } from 'react-router'

/** The panels that open over the page without an address of their own (decision 2026-11-01): H2 and H1. */
export type OverlayName = 'search' | 'ask'

interface OverlayState {
  overlay?: OverlayName
}

/**
 * Search and the AI chat open over the current page. Opening adds a step to the browser history (same address), so
 * the phone's Back button closes the panel instead of leaving the page.
 */
export function useOverlay() {
  const location = useLocation()
  const navigate = useNavigate()
  const current = (location.state as OverlayState | null)?.overlay ?? null

  const open = useCallback(
    (name: OverlayName) => {
      if (current === name) {
        return
      }
      const here = { pathname: location.pathname, search: location.search, hash: location.hash }
      // Switching from one panel to the other replaces the step, so one Back still closes it.
      navigate(here, {
        state: { overlay: name },
        replace: current !== null,
        preventScrollReset: true,
      })
    },
    [current, location.pathname, location.search, location.hash, navigate],
  )

  const close = useCallback(() => {
    if (!current) {
      return
    }
    if (location.key !== 'default') {
      navigate(-1)
    } else {
      // Opened on a fresh page load (nothing to go back to): just drop the panel.
      const here = { pathname: location.pathname, search: location.search, hash: location.hash }
      navigate(here, { replace: true, state: null, preventScrollReset: true })
    }
  }, [current, location.key, location.pathname, location.search, location.hash, navigate])

  return { current, open, close }
}
