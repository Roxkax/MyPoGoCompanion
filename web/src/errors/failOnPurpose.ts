import type { EnvironmentKind } from '../environmentKind'

// QA and local copies only (E01-F08): `?fail=` makes an error on purpose, to see the error screens and find the
// report in Application Insights. `screen` breaks the screen ("Something went wrong"), `script` throws an error
// nobody catches (reported, nothing shown), `api` shows "isn't answering" as if the API had stopped. Never in
// production.

export const failParam = 'fail'

export type FailOnPurpose = 'screen' | 'script' | 'api'

const kinds: readonly string[] = ['screen', 'script', 'api'] satisfies readonly FailOnPurpose[]

export function readFail(kind: EnvironmentKind, search: string): FailOnPurpose | null {
  if (kind === 'production') {
    return null
  }
  const asked = new URLSearchParams(search).get(failParam)
  return asked !== null && kinds.includes(asked) ? (asked as FailOnPurpose) : null
}

/** The error a `?fail=` makes: clearly a test, so nobody hunts for a real bug. */
export class FailOnPurposeError extends Error {
  constructor(kind: FailOnPurpose) {
    super(`Test error made on purpose with ?fail=${kind}`)
    this.name = 'FailOnPurposeError'
  }
}
