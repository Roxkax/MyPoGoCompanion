import '@fontsource-variable/nunito'
import './index.css'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './App'
import { applyEnvironment, readEnvironment } from './environment'

const queryClient = new QueryClient()
const environment = readEnvironment()
applyEnvironment(environment)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App environment={environment} />
    </QueryClientProvider>
  </StrictMode>,
)
