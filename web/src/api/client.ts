import createFetchClient, { type Middleware } from 'openapi-fetch'
import createQueryHooks from 'openapi-react-query'
import { referenceFromTraceId } from '../errors/reference'
import type { components, paths } from './generated/schema'

// The API client, typed from the API's OpenAPI document (api/openapi.json → generated/schema.ts, `npm run api:generate`).
// Calls name the address: `api.GET('/me')`, and in components `$api.useQuery('get', '/health')`. Paths, parameters,
// bodies and answers are all checked against the document by the type check (docs/API.md).

/** The API's request and response types, by their name in the document (e.g. `Schemas['MeResponse']`). */
export type Schemas = components['schemas']

/** An error answer: problem details with a `detail` written for people (RFC 9457). */
export type Problem = Schemas['ProblemDetails']

/** The API's address: VITE_API_URL at build time, or this page's own origin when it's empty. */
export function apiBaseUrl(
  base: string = import.meta.env.VITE_API_URL ?? '',
  origin: string = globalThis.location?.origin ?? '',
): string {
  return (base || origin).replace(/\/+$/, '')
}

/**
 * A request the API refused, with its status (401 means "sign in again"), the API's own message and the error's
 * reference (the start of the API's trace id, to find it in Application Insights; null when the answer had none).
 */
export class ApiError extends Error {
  readonly status: number
  readonly reference: string | null

  constructor(status: number, message: string, reference: string | null = null) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.reference = reference
  }
}

/** The answer's data, or an {@link ApiError} with the problem's `detail` (or the status) when the API refused. */
export function dataOrThrow<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (result.response.ok) {
    return result.data as T
  }
  const problem = result.error as (Problem & { traceId?: unknown }) | undefined
  throw new ApiError(
    result.response.status,
    problem?.detail ?? `The API answered ${result.response.status}`,
    referenceFromTraceId(problem?.traceId),
  )
}

/**
 * Sign-in endpoints that work with Google's code or the refresh token, never with an access token: asking the session
 * for a token there would refresh in the middle of a refresh. Every other request gets the token when signed in.
 */
const tokenFreePaths = new Set<string>([
  '/auth/google/client',
  '/auth/google',
  '/auth/refresh',
  '/auth/sign-out',
])

/** Whether a request to this path (as written in the document) carries the access token. */
export function sendsToken(schemaPath: string): boolean {
  return !tokenFreePaths.has(schemaPath)
}

/** Adds `Authorization: Bearer <access token>` when signed in (the session refreshes the token when it's due). */
export function bearerToken(getAccessToken: () => Promise<string | null>): Middleware {
  return {
    async onRequest({ request, schemaPath }) {
      if (!sendsToken(schemaPath) || request.headers.has('Authorization')) {
        return undefined
      }
      const token = await getAccessToken()
      if (token) {
        request.headers.set('Authorization', `Bearer ${token}`)
      }
      return request
    },
  }
}

/** A client for the API at `baseUrl`. `fetch` is looked up on each call, so tests can stand in for the network. */
export function createApiClient(baseUrl: string = apiBaseUrl()) {
  return createFetchClient<paths>({
    baseUrl,
    fetch: (request: Request) => globalThis.fetch(request),
    headers: { Accept: 'application/json' },
  })
}

/** The app's API client. The sign-in adds the access token to it (AuthProvider). */
export const api = createApiClient()

/** TanStack Query hooks on the same client: `$api.useQuery('get', '/health')`, `$api.useMutation('put', '/me/language')`. */
export const $api = createQueryHooks(api)
