import { useCallback, useState } from 'react'
import { useAuth } from './auth/authContext'
import { callbackPath } from './auth/google'
import { ApiStatus } from './components/ApiStatus'
import { AuthCallback } from './components/AuthCallback'
import { EnvironmentBanner } from './components/EnvironmentBanner'
import { ProfileButton } from './components/ProfileButton'
import { SignedOutPrompt } from './components/SignedOutPrompt'
import { SignInDialog } from './components/SignInDialog'
import { readEnvironment, type AppEnvironment } from './environment'

export function App({ environment = readEnvironment() }: { environment?: AppEnvironment }) {
  const { state } = useAuth()
  const [path, setPath] = useState(() => window.location.pathname)
  const leaveCallback = useCallback((to: string) => {
    window.history.replaceState(null, '', to)
    setPath(window.location.pathname)
  }, [])

  return (
    <div className="shell">
      <EnvironmentBanner environment={environment} />
      <header className="topbar">
        <img src={environment.icon} alt="" width="28" height="28" />
        <span className="brand">MyPoGoCompanion</span>
        {environment.label && (
          <span className="env-pill" data-env={environment.kind}>
            {environment.label}
          </span>
        )}
        <span className="topbar-end">
          <ProfileButton />
        </span>
      </header>
      <main className="content">
        {path === callbackPath ? (
          <AuthCallback onDone={leaveCallback} />
        ) : (
          <>
            <h1>Coming soon</h1>
            <p className="lead">
              A free companion for Pokémon GO: Pokédex, raid counters, search filters and your
              collection. The app is being built; this page checks that everything is connected.
            </p>
            <ApiStatus />
            {/* Until the Collection tab exists (E01-F03), its signed-out prompt shows here. */}
            {state.status === 'signedOut' && <SignedOutPrompt feature="collection" />}
            {state.status === 'signedIn' && (
              <section className="card signed-in" aria-label="Signed in">
                <p className="card-label">Signed in</p>
                <p className="signed-in-name">{state.user.name}</p>
                <p className="signed-in-detail">{state.user.email}</p>
              </section>
            )}
          </>
        )}
      </main>
      <footer className="footer">
        Unofficial fan project. Not affiliated with Niantic, Nintendo, Creatures, Game Freak or The
        Pokémon Company.
      </footer>
      <SignInDialog />
    </div>
  )
}
