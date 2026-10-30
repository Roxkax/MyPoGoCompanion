import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiUrl, fetchHealth } from './health'

afterEach(() => vi.unstubAllGlobals())

function stubFetch(status: number, body: unknown) {
  const fetchMock = vi.fn(() => Promise.resolve(Response.json(body, { status })))
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('apiUrl', () => {
  it('joins the base address and the path without doubling slashes', () => {
    expect(apiUrl('/health', 'https://api.example/')).toBe('https://api.example/health')
    expect(apiUrl('/health', 'https://api.example')).toBe('https://api.example/health')
  })

  it('uses the same origin when no base address is set', () => {
    expect(apiUrl('/health', '')).toBe('/health')
  })
})

describe('fetchHealth', () => {
  it('returns the report from a healthy API', async () => {
    const body = { status: 'Healthy', version: 'abc1234', checks: { database: 'Healthy' } }
    const fetchMock = stubFetch(200, body)

    await expect(fetchHealth()).resolves.toEqual(body)
    expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/health$/), expect.anything())
  })

  it('returns the report when the API answers 503 because the database is down', async () => {
    const body = { status: 'Unhealthy', version: 'abc1234', checks: { database: 'Unhealthy' } }
    stubFetch(503, body)

    await expect(fetchHealth()).resolves.toEqual(body)
  })

  it('fails on any other error status', async () => {
    stubFetch(500, { title: 'Server error' })

    await expect(fetchHealth()).rejects.toThrow('The API answered 500')
  })
})
