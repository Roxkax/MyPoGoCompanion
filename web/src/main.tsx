import '@fontsource-variable/nunito'
import '@fontsource/jetbrains-mono/latin-400.css'
import './index.css'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './App'
import { api } from './api/client'
import { AuthProvider } from './auth/AuthProvider'
import { applyEnvironment, readEnvironment } from './environment'
import { ErrorReporter, listenForErrors } from './errors/reporter'
import { ErrorReporterContext } from './errors/reporterContext'
import { LanguageProvider } from './i18n/LanguageProvider'
import { ConnectionWatch, connectionMiddleware } from './pwa/connection'
import { InstallWatch } from './pwa/install'
import { InstallHintStore } from './pwa/installHint'
import type { Pwa } from './pwa/pwaContext'
import { PwaProvider } from './pwa/PwaProvider'
import { registerServiceWorker } from './pwa/register'
import { ThemeProvider } from './theme/ThemeProvider'

const queryClient = new QueryClient()
const environment = readEnvironment()
applyEnvironment(environment)

// Errors nobody handled are reported to the API (E01-F08), from the first moment.
const errorReporter = new ErrorReporter()
listenForErrors(errorReporter)

// Installable app and offline screen (E01-F06): started before the app draws, as the browser may announce early that
// the app can be installed, and a failed API request must be able to tell that the connection is gone.
const pwa: Pwa = {
  install: new InstallWatch(),
  hint: new InstallHintStore(),
  connection: new ConnectionWatch(),
}
pwa.install.start()
pwa.connection.start()
pwa.hint.countVisit()
api.use(connectionMiddleware(pwa.connection))
registerServiceWorker()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ErrorReporterContext value={errorReporter}>
      <ThemeProvider>
        <LanguageProvider>
          <QueryClientProvider client={queryClient}>
            <AuthProvider>
              <PwaProvider pwa={pwa}>
                <App environment={environment} />
              </PwaProvider>
            </AuthProvider>
          </QueryClientProvider>
        </LanguageProvider>
      </ThemeProvider>
    </ErrorReporterContext>
  </StrictMode>,
)
