import '@fontsource-variable/nunito'
import '@fontsource/jetbrains-mono/latin-400.css'
import './index.css'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './App'
import { AuthProvider } from './auth/AuthProvider'
import { applyEnvironment, readEnvironment } from './environment'
import { ThemeProvider } from './theme/ThemeProvider'

const queryClient = new QueryClient()
const environment = readEnvironment()
applyEnvironment(environment)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <App environment={environment} />
        </AuthProvider>
      </QueryClientProvider>
    </ThemeProvider>
  </StrictMode>,
)
