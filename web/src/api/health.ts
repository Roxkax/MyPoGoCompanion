import type { Schemas } from './client'

/** What GET /health returns. */
export type HealthReport = Schemas['HealthResponse']

/** The API's 503 answer still carries the report: the API is running but something it needs is not. */
export function isHealthReport(value: unknown): value is HealthReport {
  return typeof value === 'object' && value !== null && 'status' in value && 'checks' in value
}
