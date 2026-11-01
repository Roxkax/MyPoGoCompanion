import { ApiStatus } from './components/ApiStatus'
import { EnvironmentBanner } from './components/EnvironmentBanner'
import { readEnvironment, type AppEnvironment } from './environment'

export function App({ environment = readEnvironment() }: { environment?: AppEnvironment }) {
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
      </header>
      <main className="content">
        <h1>Coming soon</h1>
        <p className="lead">
          A free companion for Pokémon GO: Pokédex, raid counters, search filters and your
          collection. The app is being built; this page checks that everything is connected.
        </p>
        <ApiStatus />
      </main>
      <footer className="footer">
        Unofficial fan project. Not affiliated with Niantic, Nintendo, Creatures, Game Freak or The
        Pokémon Company.
      </footer>
    </div>
  )
}
