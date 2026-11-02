import { createContext, useContext } from 'react'
import type { ErrorReporter } from './reporter'

/** Sends nothing: the default outside the app (tests), so a test only reports when it gives its own reporter. */
const silent: Pick<ErrorReporter, 'report'> = {
  report: (_kind, _error, reference) => reference ?? '00000000',
}

/** The app's error reporter (main.tsx gives the real one). */
export const ErrorReporterContext = createContext<Pick<ErrorReporter, 'report'>>(silent)

export function useErrorReporter(): Pick<ErrorReporter, 'report'> {
  return useContext(ErrorReporterContext)
}
