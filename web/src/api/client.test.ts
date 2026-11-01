/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { readRequest, type SentRequest } from '../testing/fetch'
import {
  ApiError,
  apiBaseUrl,
  bearerToken,
  createApiClient,
  dataOrThrow,
  sendsToken,
} from './client'

afterEach(() => vi.unstubAllGlobals())

type Operation = { security?: unknown[] }
// The API's committed OpenAPI document (api/openapi.json).
const here = dirname(fileURLToPath(import.meta.url))
const document = JSON.parse(readFileSync(resolve(here, '../../../api/openapi.json'), 'utf8')) as {
  paths: Record<string, Record<string, Operation>>
}

function capture(response: () => Response = () => Response.json({})) {
  const calls: SentRequest[] = []
  vi.stubGlobal('fetch', async (input: RequestInfo | URL, init?: RequestInit) => {
    calls.push(await readRequest(input, init))
    return response()
  })
  return calls
}

describe('apiBaseUrl', () => {
  it('uses the build’s API address without a trailing slash', () => {
    expect(apiBaseUrl('https://api.example/', 'https://web.example')).toBe('https://api.example')
  })

  it('falls back to the page’s own origin when no address is set', () => {
    expect(apiBaseUrl('', 'https://web.example')).toBe('https://web.example')
    expect(apiBaseUrl()).toBe(window.location.origin)
  })
})

describe('dataOrThrow', () => {
  it('returns the data of a successful answer', () => {
    expect(dataOrThrow({ data: { clientId: 'x' }, response: new Response(null) })).toEqual({
      clientId: 'x',
    })
  })

  it('throws an ApiError with the problem’s detail', () => {
    const refusal = {
      error: { detail: 'The language must be one of: en, pt-PT.' },
      response: new Response(null, { status: 400 }),
    }

    expect(() => dataOrThrow(refusal)).toThrow(ApiError)
    expect(() => dataOrThrow(refusal)).toThrow('The language must be one of: en, pt-PT.')
  })

  it('falls back to the status when the refusal has no detail', () => {
    const refusal = { error: 'oops', response: new Response(null, { status: 502 }) }

    expect(() => dataOrThrow(refusal)).toThrow('The API answered 502')
  })
})

describe('the generated client', () => {
  it('calls the API at its address with the typed path, parameters and body', async () => {
    const calls = capture(() => Response.json([]))
    const client = createApiClient('https://api.example')

    await client.GET('/admin/users', { params: { query: { search: 'ash', limit: 5 } } })
    await client.PUT('/admin/users/{id}/role', {
      params: { path: { id: '0f8c2d1e-5b7a-4c39-9e61-2a4d8b3f7c10' } },
      body: { role: 'Admin' },
    })

    expect(calls[0]!.url).toBe('https://api.example/admin/users?search=ash&limit=5')
    expect(calls[0]!.headers.get('Accept')).toBe('application/json')
    expect(calls[1]).toMatchObject({
      method: 'PUT',
      url: 'https://api.example/admin/users/0f8c2d1e-5b7a-4c39-9e61-2a4d8b3f7c10/role',
      body: { role: 'Admin' },
    })
  })
})

describe('the access token', () => {
  it('is sent wherever the API document asks for it, and never to the token endpoints', () => {
    for (const [path, operations] of Object.entries(document.paths)) {
      for (const [method, operation] of Object.entries(operations)) {
        if (operation.security) {
          expect(sendsToken(path), `${method} ${path} needs the token`).toBe(true)
        }
      }
    }
    for (const path of ['/auth/google/client', '/auth/google', '/auth/refresh', '/auth/sign-out']) {
      expect(document.paths[path], `${path} is in the document`).toBeDefined()
      expect(sendsToken(path)).toBe(false)
    }
  })

  it('is added to requests while signed in', async () => {
    const calls = capture()
    const client = createApiClient('https://api.example')
    client.use(bearerToken(() => Promise.resolve('access-1')))

    await client.GET('/me')

    expect(calls[0]!.headers.get('Authorization')).toBe('Bearer access-1')
  })

  it('isn’t asked for on the token endpoints, so a refresh never waits on itself', async () => {
    const calls = capture()
    const getAccessToken = vi.fn(() => Promise.resolve('access-1'))
    const client = createApiClient('https://api.example')
    client.use(bearerToken(getAccessToken))

    await client.POST('/auth/refresh', { body: { refreshToken: 'rt' } })

    expect(getAccessToken).not.toHaveBeenCalled()
    expect(calls[0]!.headers.has('Authorization')).toBe(false)
  })

  it('leaves a token the caller set, and sends none when signed out', async () => {
    const calls = capture()
    const client = createApiClient('https://api.example')
    client.use(bearerToken(() => Promise.resolve(null)))

    await client.GET('/me', { headers: { Authorization: 'Bearer given' } })
    await client.GET('/me')

    expect(calls[0]!.headers.get('Authorization')).toBe('Bearer given')
    expect(calls[1]!.headers.has('Authorization')).toBe(false)
  })
})
