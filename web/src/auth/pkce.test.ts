import { describe, expect, it } from 'vitest'
import { base64Url, challengeFor, randomString } from './pkce'

describe('PKCE', () => {
  it('makes verifiers of RFC 7636 characters, different each time', () => {
    const verifier = randomString()

    expect(verifier).toMatch(/^[A-Za-z0-9\-._~]{64}$/)
    expect(randomString()).not.toBe(verifier)
    expect(randomString(43)).toHaveLength(43)
  })

  it('computes the S256 challenge from RFC 7636 appendix B', async () => {
    expect(await challengeFor('dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk')).toBe(
      'E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM',
    )
  })

  it('encodes base64url without padding', () => {
    expect(base64Url(new Uint8Array([251, 255]))).toBe('-_8')
  })
})
