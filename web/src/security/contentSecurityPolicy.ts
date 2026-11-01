// The Content Security Policy the built app is served with (Static Web Apps' staticwebapp.config.json). It is what
// keeps the refresh token in localStorage reasonably safe (decision 2026-11-01): only our own scripts run, and the
// page can only talk to itself and the API. Written at build time because the API's address differs per environment.

/** Google profile pictures, shown in the profile button. */
export const googlePictures = 'https://lh3.googleusercontent.com'

/** The policy for a build whose API lives at `apiUrl` (empty: the same origin as the web app). */
export function contentSecurityPolicy(apiUrl: string | undefined): string {
  const api = apiUrl ? new URL(apiUrl).origin : null
  const directives: Record<string, string[]> = {
    'default-src': ["'self'"],
    'script-src': ["'self'"],
    'style-src': ["'self'"],
    'img-src': ["'self'", 'data:', googlePictures],
    'font-src': ["'self'", 'data:'],
    'connect-src': api ? ["'self'", api] : ["'self'"],
    'object-src': ["'none'"],
    'base-uri': ["'self'"],
    'form-action': ["'self'"],
    'frame-ancestors': ["'none'"],
  }
  return Object.entries(directives)
    .map(([name, values]) => `${name} ${values.join(' ')}`)
    .join('; ')
}

/** Adds the policy (and framing protection) to the Static Web Apps configuration's global headers. */
export function withContentSecurityPolicy(configJson: string, apiUrl: string | undefined): string {
  const config = JSON.parse(configJson) as { globalHeaders?: Record<string, string> }
  config.globalHeaders = {
    ...config.globalHeaders,
    'Content-Security-Policy': contentSecurityPolicy(apiUrl),
    'X-Frame-Options': 'DENY',
  }
  return `${JSON.stringify(config, null, 2)}\n`
}
