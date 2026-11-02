import { $api } from '../api/client'
import { applyOverride, type Freshness } from './freshness'
import { useStaleOverride } from './freshnessContext'

/** How often an open app asks again: the warning works in days, so half an hour is plenty. */
export const refreshEvery = 30 * 60 * 1000

/** The API answers may be reused for five minutes (it says so too: Cache-Control max-age=300). */
export const freshFor = 5 * 60 * 1000

/**
 * How up to date the data is: asked once when the app opens (every screen shares the answer) and again every half
 * hour. When the API can't be reached there is no warning: the offline screen and the screens' own errors cover that.
 */
export function useFreshness(): Freshness | undefined {
  const override = useStaleOverride()
  const query = $api.useQuery('get', '/freshness', undefined, {
    staleTime: freshFor,
    refetchInterval: refreshEvery,
    retry: 1,
  })
  return applyOverride(query.data, override)
}
