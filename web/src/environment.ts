/**
 * Which copy of the app this is, so QA is never mistaken for the real app. Set at build time:
 * VITE_APP_ENV is "production" (prod deploy), "qa" (QA deploy) or "local" (dev server, and any build that
 * doesn't say: an unmarked build shows "Local" rather than looking like prod). The QA deploy also sets
 * VITE_PR_NUMBER and VITE_APP_VERSION (the commit) so the banner says what QA is showing.
 */
export type EnvironmentKind = 'production' | 'qa' | 'local'

export interface AppEnvironment {
  kind: EnvironmentKind
  /** Short name shown in the banner and the tab title; null in production. */
  label: string | null
  /** The banner's second part (e.g. "PR #920 · abc1234"); null when there is nothing to add. */
  detail: string | null
  /** The browser tab's title. */
  title: string
  /** The browser tab's icon. */
  icon: string
}

export interface EnvironmentSettings {
  VITE_APP_ENV?: string
  VITE_PR_NUMBER?: string
  VITE_APP_VERSION?: string
}

const appName = 'MyPoGoCompanion'

export function readEnvironment(settings: EnvironmentSettings = import.meta.env): AppEnvironment {
  const value = (settings.VITE_APP_ENV ?? '').trim().toLowerCase()
  const kind: EnvironmentKind = value === 'production' || value === 'qa' ? value : 'local'

  if (kind === 'production') {
    return { kind, label: null, detail: null, title: appName, icon: '/icon.svg' }
  }

  const label = kind === 'qa' ? 'QA' : 'Local'
  const parts = [
    settings.VITE_PR_NUMBER ? `PR #${settings.VITE_PR_NUMBER}` : null,
    settings.VITE_APP_VERSION || null,
  ].filter((part): part is string => part !== null)
  return {
    kind,
    label,
    detail: parts.length > 0 ? parts.join(' · ') : null,
    title: `${label} · ${appName}`,
    icon: kind === 'qa' ? '/icon-qa.svg' : '/icon-local.svg',
  }
}

/** Marks the page itself: tab title, tab icon and a data-env attribute on <html>. */
export function applyEnvironment(environment: AppEnvironment, doc: Document = document): void {
  doc.title = environment.title
  doc.documentElement.dataset.env = environment.kind
  const icon = doc.querySelector<HTMLLinkElement>('link[rel="icon"]')
  if (icon) {
    icon.href = environment.icon
  }
}
