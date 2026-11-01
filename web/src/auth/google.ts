import { fetchGoogleClientId, signInWithGoogle, type SessionResponse } from './authApi'
import { challengeFor, randomString } from './pkce'
import { takePendingSignIn, writePendingSignIn } from './storage'

export const googleAuthorizeUrl = 'https://accounts.google.com/o/oauth2/v2/auth'
export const callbackPath = '/auth/callback'

/** Where Google sends people back: this copy of the app's own address. */
export function redirectUri(origin: string = window.location.origin): string {
  return origin + callbackPath
}

/** Only a path on this site (never "//elsewhere" or a full address), so the return can't be used to send people away. */
export function safeReturnTo(value: string | null | undefined): string {
  return value && value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/\\')
    ? value
    : '/'
}

/** The address to send the browser to: Google's sign-in with our client, PKCE challenge and a random state. */
export async function buildGoogleSignInUrl(
  returnTo: string,
  origin: string = window.location.origin,
): Promise<string> {
  const clientId = await fetchGoogleClientId()
  const verifier = randomString(64)
  const state = randomString(32)
  writePendingSignIn({ state, verifier, returnTo: safeReturnTo(returnTo) })
  const params = new URLSearchParams({
    client_id: clientId,
    redirect_uri: redirectUri(origin),
    response_type: 'code',
    scope: 'openid email profile',
    state,
    code_challenge: await challengeFor(verifier),
    code_challenge_method: 'S256',
    prompt: 'select_account',
  })
  return `${googleAuthorizeUrl}?${params}`
}

/** Why coming back from Google didn't sign the person in (the screen shows `reason` in the app's language). */
export class SignInError extends Error {
  readonly reason: 'cancelled' | 'expired'

  constructor(reason: 'cancelled' | 'expired', message: string) {
    super(message)
    this.name = 'SignInError'
    this.reason = reason
  }
}

/**
 * Finishes the sign-in on the callback page: checks the state, then lets the API swap the code for our tokens.
 * `language` is the one the device is showing, for a new account.
 */
export async function completeGoogleSignIn(
  search: string,
  origin: string = window.location.origin,
  language?: string,
): Promise<{ session: SessionResponse; returnTo: string }> {
  const params = new URLSearchParams(search)
  const pending = takePendingSignIn()
  if (params.get('error')) {
    // e.g. access_denied when the person closes Google's screen.
    throw new SignInError('cancelled', 'Sign-in was cancelled.')
  }
  const code = params.get('code')
  if (!pending || !code || params.get('state') !== pending.state) {
    throw new SignInError('expired', 'This sign-in link has expired. Please try again.')
  }
  const session = await signInWithGoogle(code, pending.verifier, redirectUri(origin), language)
  return { session, returnTo: pending.returnTo }
}
