import { createContext, useContext } from 'react'
import type { StaleOverride } from './freshness'

/** The QA/local pretend-old setting (`?stale=`), worked out once by the shell; null everywhere else. */
export const StaleOverrideContext = createContext<StaleOverride | null>(null)

export function useStaleOverride(): StaleOverride | null {
  return useContext(StaleOverrideContext)
}
