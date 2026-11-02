import { ApiError, api, type Schemas } from '../api/client'
import { newReference } from './reference'

// Error reporting (decision 2026-11-02): screen breaks and script errors nobody handled are sent to the API
// (`POST /client-errors`), which logs them to Application Insights with personal data removed. No browser SDK, no page
// views, no tracking. The app shows the report's reference so the owner can find it.

export type ErrorReport = Schemas['ClientErrorRequest']
export type ErrorKind = NonNullable<ErrorReport['kind']>

/** At most this many reports per page load (a broken loop must not flood the API; it limits per address anyway). */
export const maxReportsPerPage = 10

/** How much of a message and a stack trace is sent (the API cuts longer ones too). */
export const maxMessageLength = 1_000
export const maxStackLength = 8_000

/** The message and stack trace of anything thrown. */
export function describeError(error: unknown): { message: string; stack: string | null } {
  if (error instanceof Error) {
    const message = error.message || error.name || 'Error'
    return { message, stack: error.stack ?? null }
  }
  if (typeof error === 'string') {
    return { message: error || 'Error', stack: null }
  }
  try {
    return { message: JSON.stringify(error) ?? String(error), stack: null }
  } catch {
    return { message: String(error), stack: null }
  }
}

/**
 * Errors not worth a report: the API's own refusals (the API records those itself), requests that got no answer
 * (the offline and API screens deal with them), another site's script (the browser hides what went wrong) and a
 * harmless browser warning.
 */
export function isIgnored(error: unknown, message: string): boolean {
  if (error instanceof ApiError) {
    return true
  }
  if (error instanceof TypeError && /failed to fetch|networkerror|load failed/i.test(message)) {
    return true
  }
  return /^script error\.?$/i.test(message) || /resizeobserver loop/i.test(message)
}

export interface ReporterOptions {
  send?: (report: ErrorReport) => Promise<unknown>
  path?: () => string
  version?: string
  reference?: () => string
}

export class ErrorReporter {
  private readonly send: (report: ErrorReport) => Promise<unknown>
  private readonly path: () => string
  private readonly version: string
  private readonly reference: () => string
  private readonly seen = new Set<string>()
  private sent = 0

  constructor(options: ReporterOptions = {}) {
    this.send = options.send ?? ((body) => api.POST('/client-errors', { body }))
    this.path = options.path ?? (() => globalThis.location?.pathname ?? '/')
    this.version = options.version ?? (import.meta.env.VITE_APP_VERSION || 'dev')
    this.reference = options.reference ?? (() => newReference())
  }

  /**
   * Reports an error and returns its reference (also when it isn't sent: ignored, repeated or over the limit, the
   * reference still shows on screen). Reporting never throws.
   */
  report(kind: ErrorKind, error: unknown, reference: string = this.reference()): string {
    const { message, stack } = describeError(error)
    const key = `${kind}|${message}|${stack ?? ''}`
    if (isIgnored(error, message) || this.seen.has(key) || this.sent >= maxReportsPerPage) {
      return reference
    }
    this.seen.add(key)
    this.sent += 1
    const report: ErrorReport = {
      reference,
      kind,
      message: message.slice(0, maxMessageLength),
      stack: stack?.slice(0, maxStackLength) ?? null,
      path: this.path(),
      appVersion: this.version,
    }
    try {
      void this.send(report).catch(() => undefined)
    } catch {
      // Reporting must never break the app.
    }
    return reference
  }
}

/** Reports script errors and failed promises nobody handled; returns a function that stops listening. */
export function listenForErrors(
  reporter: Pick<ErrorReporter, 'report'>,
  target: Pick<Window, 'addEventListener' | 'removeEventListener'> = window,
): () => void {
  const onError = (event: ErrorEvent) => reporter.report('Script', event.error ?? event.message)
  const onRejection = (event: PromiseRejectionEvent) => reporter.report('Script', event.reason)
  target.addEventListener('error', onError)
  target.addEventListener('unhandledrejection', onRejection)
  return () => {
    target.removeEventListener('error', onError)
    target.removeEventListener('unhandledrejection', onRejection)
  }
}
