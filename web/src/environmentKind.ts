// Which copy of the app a build is (see environment.ts). Kept apart from the browser code so the build
// configuration (vite.config.ts) can read it too.

export type EnvironmentKind = 'production' | 'qa' | 'local'

/** VITE_APP_ENV's value: "production", "qa", or anything else (nothing at all included) counts as local. */
export function environmentKind(value: string | undefined): EnvironmentKind {
  const kind = (value ?? '').trim().toLowerCase()
  return kind === 'production' || kind === 'qa' ? kind : 'local'
}
