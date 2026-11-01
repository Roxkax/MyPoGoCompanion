import { versionName, type VersionKind } from './pokemonTypes'

/** One version of a Pokémon on its line (Mega, Shadow, Dynamax, G-Max, regional forms). */
export function VersionMarker({ kind, label }: { kind: VersionKind; label?: string }) {
  return (
    <span className="ui-version" data-kind={kind}>
      {label ?? versionName[kind]}
    </span>
  )
}
