import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { initReactI18next } from 'react-i18next'
import { afterEach } from 'vitest'
import { createI18n } from './i18n/i18n'

// Components drawn on their own (without LanguageProvider) get the English texts, as react-i18next's default.
initReactI18next.init(createI18n('en'))

// jsdom has no scrolling; React Router's scroll restoration calls it on every navigation.
window.scrollTo = () => {}

afterEach(() => {
  cleanup()
  // A pretend screen size (src/testing/app.tsx setViewport) doesn't leak into the next test.
  delete (window as { matchMedia?: unknown }).matchMedia
  // Nor a theme picked in the test.
  delete document.documentElement.dataset.theme
  // Nor a language.
  document.documentElement.removeAttribute('lang')
  window.history.replaceState(null, '', '/')
})
