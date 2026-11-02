// An error's reference (E01-F08): 8 hex characters shown under an error so the owner can find it in Application
// Insights. For an API error it is the start of the API's trace id (problem details' `traceId`, the request's
// operation id there); for a screen or script error the app makes one and sends it with the report.

/** How many hex characters a reference shows. */
export const referenceLength = 8

/** Fills the bytes with random values (the browser's crypto; tests pass their own). */
export type RandomFill = (bytes: Uint8Array<ArrayBuffer>) => Uint8Array<ArrayBuffer>

/** A new random reference (8 hex characters). */
export function newReference(
  random: RandomFill = (bytes) => crypto.getRandomValues(bytes),
): string {
  return [...random(new Uint8Array(referenceLength / 2))]
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('')
}

/**
 * The reference in an API trace id: ASP.NET Core writes the W3C form `00-<32 hex trace id>-<16 hex>-<flags>`; a bare
 * 32-hex trace id works too. Null when there is none.
 */
export function referenceFromTraceId(traceId: unknown): string | null {
  if (typeof traceId !== 'string') {
    return null
  }
  const match = /^(?:[0-9a-f]{2}-)?([0-9a-f]{32})(?:-|$)/.exec(traceId.trim().toLowerCase())
  return match?.[1]?.slice(0, referenceLength) ?? null
}
