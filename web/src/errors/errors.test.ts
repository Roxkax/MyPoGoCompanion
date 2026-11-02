import { describe, expect, it, vi } from 'vitest'
import { ApiError, dataOrThrow } from '../api/client'
import { FailOnPurposeError, readFail } from './failOnPurpose'
import { newReference, referenceFromTraceId } from './reference'
import {
  describeError,
  ErrorReporter,
  isIgnored,
  listenForErrors,
  maxMessageLength,
  maxReportsPerPage,
  maxStackLength,
  type ErrorReport,
} from './reporter'

describe('references', () => {
  it('makes 8 hex characters from random bytes', () => {
    expect(newReference((bytes) => bytes.fill(0xab))).toBe('abababab')
    expect(newReference()).toMatch(/^[0-9a-f]{8}$/)
  })

  it('takes the start of the API’s trace id, in the W3C form or bare', () => {
    expect(referenceFromTraceId('00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01')).toBe(
      '4bf92f35',
    )
    expect(referenceFromTraceId('4BF92F3577B34DA6A3CE929D0E0E4736')).toBe('4bf92f35')
    expect(referenceFromTraceId('not a trace id')).toBeNull()
    expect(referenceFromTraceId(undefined)).toBeNull()
    expect(referenceFromTraceId(42)).toBeNull()
  })

  it('an API error carries the reference of the API’s answer', () => {
    const error = (() => {
      try {
        dataOrThrow({
          error: {
            detail: 'Boom',
            traceId: '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00',
          },
          response: new Response(null, { status: 500 }),
        })
      } catch (caught) {
        return caught
      }
    })()
    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).reference).toBe('4bf92f35')
    expect(new ApiError(500, 'x').reference).toBeNull()
  })
})

describe('describeError', () => {
  it('reads errors, text and anything else thrown', () => {
    const error = new Error('Boom')
    expect(describeError(error)).toEqual({ message: 'Boom', stack: error.stack })
    expect(describeError(Object.assign(new Error(''), { stack: undefined })).message).toBe('Error')
    expect(describeError('text')).toEqual({ message: 'text', stack: null })
    expect(describeError('')).toEqual({ message: 'Error', stack: null })
    expect(describeError({ code: 7 })).toEqual({ message: '{"code":7}', stack: null })
    expect(describeError(undefined)).toEqual({ message: 'undefined', stack: null })
    const circular: Record<string, unknown> = {}
    circular.self = circular
    expect(describeError(circular).message).toBe('[object Object]')
  })
})

describe('isIgnored', () => {
  it('skips the API’s refusals, requests with no answer and what the browser hides', () => {
    expect(isIgnored(new ApiError(500, 'x'), 'x')).toBe(true)
    expect(isIgnored(new TypeError('Failed to fetch'), 'Failed to fetch')).toBe(true)
    expect(isIgnored(new TypeError('Load failed'), 'Load failed')).toBe(true)
    expect(isIgnored(null, 'Script error.')).toBe(true)
    expect(isIgnored(null, 'ResizeObserver loop completed with undelivered notifications.')).toBe(
      true,
    )
    expect(isIgnored(new TypeError('x is undefined'), 'x is undefined')).toBe(false)
  })
})

describe('ErrorReporter', () => {
  const reporterWith = () => {
    const send = vi.fn((report: ErrorReport) => Promise.resolve(report))
    const reporter = new ErrorReporter({
      send,
      path: () => '/pokedex',
      version: '3cd4b0c',
      reference: () => '7f3a2c91',
    })
    return { reporter, send }
  }

  it('sends the report with its reference, the address and the version, and returns the reference', () => {
    const { reporter, send } = reporterWith()
    const error = new TypeError('x is undefined')

    expect(reporter.report('Screen', error)).toBe('7f3a2c91')

    expect(send).toHaveBeenCalledWith({
      reference: '7f3a2c91',
      kind: 'Screen',
      message: 'x is undefined',
      stack: error.stack,
      path: '/pokedex',
      appVersion: '3cd4b0c',
    })
  })

  it('uses the reference it is given', () => {
    const { reporter, send } = reporterWith()
    expect(reporter.report('Script', 'Boom', 'abcdef12')).toBe('abcdef12')
    expect(send.mock.calls[0]?.[0].reference).toBe('abcdef12')
  })

  it('sends the same error once, ignored ones never, and stops after the limit', () => {
    const { reporter, send } = reporterWith()
    reporter.report('Script', 'Boom')
    reporter.report('Script', 'Boom')
    reporter.report('Script', new ApiError(500, 'x'))
    expect(send).toHaveBeenCalledOnce()
    for (let i = 0; i < maxReportsPerPage + 5; i++) {
      reporter.report('Script', `Error ${i}`)
    }
    expect(send).toHaveBeenCalledTimes(maxReportsPerPage)
  })

  it('cuts long messages and stack traces', () => {
    const { reporter, send } = reporterWith()
    const error = new Error('m'.repeat(5_000))
    error.stack = 's'.repeat(20_000)
    reporter.report('Script', error)
    expect(send.mock.calls[0]?.[0].message).toHaveLength(maxMessageLength)
    expect(send.mock.calls[0]?.[0].stack).toHaveLength(maxStackLength)
  })

  it('never throws, whether sending fails or throws', async () => {
    const failing = new ErrorReporter({ send: () => Promise.reject(new Error('down')) })
    expect(() => failing.report('Script', 'a')).not.toThrow()
    const throwing = new ErrorReporter({
      send: () => {
        throw new Error('down')
      },
    })
    expect(() => throwing.report('Script', 'b')).not.toThrow()
    await Promise.resolve()
  })

  it('sends to the API by default, from this address and build', () => {
    const fetch = vi.fn(() => Promise.resolve(new Response(null, { status: 202 })))
    vi.stubGlobal('fetch', fetch)
    try {
      const reference = new ErrorReporter().report('Script', 'Default send')
      expect(reference).toMatch(/^[0-9a-f]{8}$/)
      expect(fetch).toHaveBeenCalledOnce()
    } finally {
      vi.unstubAllGlobals()
    }
  })
})

describe('listenForErrors', () => {
  it('reports script errors and failed promises nobody handled, until stopped', () => {
    const target = new EventTarget() as unknown as Window
    const report = vi.fn()
    const stop = listenForErrors({ report }, target)

    const error = new Error('Boom')
    target.dispatchEvent(Object.assign(new Event('error'), { error, message: 'Boom' }))
    target.dispatchEvent(Object.assign(new Event('error'), { error: null, message: 'Only text' }))
    target.dispatchEvent(Object.assign(new Event('unhandledrejection'), { reason: 'Rejected' }))
    expect(report.mock.calls).toEqual([
      ['Script', error],
      ['Script', 'Only text'],
      ['Script', 'Rejected'],
    ])

    stop()
    target.dispatchEvent(Object.assign(new Event('error'), { error, message: 'Boom' }))
    expect(report).toHaveBeenCalledTimes(3)
  })
})

describe('?fail= (QA and local only)', () => {
  it('reads screen, script and api, never in production', () => {
    expect(readFail('qa', '?fail=screen')).toBe('screen')
    expect(readFail('local', '?fail=script')).toBe('script')
    expect(readFail('qa', '?fail=api&x=1')).toBe('api')
    expect(readFail('qa', '?fail=everything')).toBeNull()
    expect(readFail('qa', '')).toBeNull()
    expect(readFail('production', '?fail=screen')).toBeNull()
  })

  it('makes an error that says it is a test', () => {
    const error = new FailOnPurposeError('screen')
    expect(error.name).toBe('FailOnPurposeError')
    expect(error.message).toBe('Test error made on purpose with ?fail=screen')
  })
})
