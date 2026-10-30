// Until the generated OpenAPI client arrives (E01-F05), the few API calls are written by hand here.

export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy'

/** What the API's GET /health returns. */
export interface HealthResponse {
  status: HealthStatus
  version: string
  checks: Record<string, HealthStatus>
}

/** The API's address for a path; VITE_API_URL is set at build time (empty means same origin). */
export function apiUrl(path: string, base: string = import.meta.env.VITE_API_URL ?? ''): string {
  return `${base.replace(/\/+$/, '')}${path}`
}

export async function fetchHealth(signal?: AbortSignal): Promise<HealthResponse> {
  const response = await fetch(apiUrl('/health'), {
    signal,
    headers: { Accept: 'application/json' },
  })
  // 503 still carries the report: the API is running but something it needs is not.
  if (!response.ok && response.status !== 503) {
    throw new Error(`The API answered ${response.status}`)
  }
  return (await response.json()) as HealthResponse
}
