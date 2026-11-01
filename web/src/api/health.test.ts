import { describe, expect, it } from 'vitest'
import { isHealthReport } from './health'

describe('isHealthReport', () => {
  it('recognises the report the API sends with 200 and 503', () => {
    expect(
      isHealthReport({ status: 'Unhealthy', version: 'abc', checks: { database: 'Unhealthy' } }),
    ).toBe(true)
  })

  it('rejects anything else', () => {
    expect(isHealthReport(new TypeError('Failed to fetch'))).toBe(false)
    expect(isHealthReport(null)).toBe(false)
    expect(isHealthReport('Bad gateway')).toBe(false)
  })
})
