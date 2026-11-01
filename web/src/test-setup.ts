import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// jsdom has no scrolling; React Router's scroll restoration calls it on every navigation.
window.scrollTo = () => {}

afterEach(() => {
  cleanup()
  // A pretend screen size (src/testing/app.tsx setViewport) doesn't leak into the next test.
  delete (window as { matchMedia?: unknown }).matchMedia
  // Nor a theme picked in the test.
  delete document.documentElement.dataset.theme
  window.history.replaceState(null, '', '/')
})
