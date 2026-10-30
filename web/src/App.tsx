import { ApiStatus } from './components/ApiStatus'

export function App() {
  return (
    <div className="shell">
      <header className="topbar">
        <img src="/icon.svg" alt="" width="28" height="28" />
        <span className="brand">MyPoGoCompanion</span>
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
