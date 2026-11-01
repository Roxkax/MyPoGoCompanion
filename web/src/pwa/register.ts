/** Where the built service worker is served (vite-plugin-pwa writes it from src/sw.ts). */
export const serviceWorkerUrl = '/sw.js'

/**
 * Registers the service worker once the page has loaded, so it never slows the first visit down. Only built copies
 * have one (the dev server serves no /sw.js). A browser without service workers simply has no offline copy.
 */
export function registerServiceWorker(
  enabled: boolean = import.meta.env.PROD,
  win: Pick<Window, 'addEventListener' | 'navigator'> = window,
): void {
  const container = win.navigator.serviceWorker as ServiceWorkerContainer | undefined
  if (!enabled || !container) {
    return
  }
  win.addEventListener('load', () => {
    container.register(serviceWorkerUrl, { scope: '/' }).catch(() => {
      // No offline copy this time (e.g. private browsing); the app works the same online.
    })
  })
}
