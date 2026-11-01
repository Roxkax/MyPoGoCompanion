import { describe, expect, it } from 'vitest'
import { MemoryStorage } from '../testing/auth'
import {
  pendingSignInKey,
  readRefreshToken,
  takePendingSignIn,
  writePendingSignIn,
  writeRefreshToken,
} from './storage'

class BrokenStorage extends MemoryStorage {
  override getItem(): string | null {
    throw new Error('blocked')
  }
  override setItem(): void {
    throw new Error('blocked')
  }
}

describe('sign-in storage', () => {
  it('keeps and removes the refresh token', () => {
    const storage = new MemoryStorage()

    writeRefreshToken('abc', storage)
    expect(readRefreshToken(storage)).toBe('abc')
    writeRefreshToken(null, storage)
    expect(readRefreshToken(storage)).toBeNull()
  })

  it('reads a pending sign-in once', () => {
    const storage = new MemoryStorage()
    writePendingSignIn({ state: 's', verifier: 'v', returnTo: '/x' }, storage)

    expect(takePendingSignIn(storage)).toEqual({ state: 's', verifier: 'v', returnTo: '/x' })
    expect(takePendingSignIn(storage)).toBeNull()
  })

  it.each(['not json', '{"state":"s"}', 'null'])(
    'ignores a damaged pending sign-in: %s',
    (value) => {
      const storage = new MemoryStorage()
      storage.setItem(pendingSignInKey, value)

      expect(takePendingSignIn(storage)).toBeNull()
    },
  )

  it('keeps working when the browser blocks storage', () => {
    const storage = new BrokenStorage()

    expect(() => writeRefreshToken('abc', storage)).not.toThrow()
    expect(readRefreshToken(storage)).toBeNull()
    expect(() =>
      writePendingSignIn({ state: 's', verifier: 'v', returnTo: '/' }, storage),
    ).not.toThrow()
    expect(takePendingSignIn(storage)).toBeNull()
  })
})
