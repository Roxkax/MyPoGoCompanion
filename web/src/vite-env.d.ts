/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** The API's address, e.g. https://api-qa.….azurecontainerapps.io. Set at build time. */
  readonly VITE_API_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
