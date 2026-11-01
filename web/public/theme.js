// Applies the theme saved in Settings (src/theme/theme.ts, key "mpgc.theme") before the page draws, so a dark
// choice never flashes light first. A file rather than an inline script: the Content Security Policy allows only
// the app's own scripts.
try {
  var theme = localStorage.getItem('mpgc.theme')
  if (theme === 'light' || theme === 'dark') {
    document.documentElement.dataset.theme = theme
  }
} catch (e) {
  // Storage unavailable: follow the device.
}
