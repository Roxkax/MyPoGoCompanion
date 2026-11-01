// PKCE (RFC 7636) for Google's authorization-code sign-in: the browser makes a secret (the verifier), sends only its
// SHA-256 (the challenge) to Google, and hands the verifier to our API with the code, so a stolen code is useless.

const unreserved = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~'

/** A random string of RFC 7636 "unreserved" characters (64 by default; the RFC allows 43–128). */
export function randomString(length = 64, crypto: Crypto = globalThis.crypto): string {
  const bytes = crypto.getRandomValues(new Uint8Array(length))
  // 66 characters: the modulo bias is tiny and harmless for a one-time secret of this length.
  return Array.from(bytes, (byte) => unreserved[byte % unreserved.length]).join('')
}

export function base64Url(bytes: Uint8Array): string {
  let binary = ''
  bytes.forEach((byte) => (binary += String.fromCharCode(byte)))
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

/** The S256 challenge for a verifier: base64url(SHA-256(verifier)). */
export async function challengeFor(
  verifier: string,
  crypto: Crypto = globalThis.crypto,
): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier))
  return base64Url(new Uint8Array(digest))
}
