import { useState } from 'react'
import { createBrowserRouter, RouterProvider } from 'react-router'
import { appRoutes } from './app/routes'
import { readEnvironment, type AppEnvironment } from './environment'

/** The web app: every screen at its own address (src/app/routes.tsx) inside the shell. */
export function App({ environment = readEnvironment() }: { environment?: AppEnvironment }) {
  const [router] = useState(() => createBrowserRouter(appRoutes(environment)))
  return <RouterProvider router={router} />
}
