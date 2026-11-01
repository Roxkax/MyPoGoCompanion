/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** The API's address, e.g. https://api-qa.….azurecontainerapps.io. Set at build time. */
  readonly VITE_API_URL?: string
  /** Which copy of the app: "production", "qa" or "local" (the default). Set at build time (src/environment.ts). */
  readonly VITE_APP_ENV?: string
  /** QA only: the pull request QA is showing. */
  readonly VITE_PR_NUMBER?: string
  /** The commit the web app was built from (short SHA). */
  readonly VITE_APP_VERSION?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
