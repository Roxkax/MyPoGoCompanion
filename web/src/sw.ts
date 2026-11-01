// The service worker (built to /sw.js by vite-plugin-pwa, registered by src/pwa/register.ts). vite-plugin-pwa
// replaces `self.__WB_MANIFEST` with the list of the build's files, so it must stay written exactly so once the types
// are gone. The work itself is in src/pwa/worker.ts.
import { startServiceWorker, type PrecacheEntry, type WorkerScope } from './pwa/worker'

startServiceWorker(
  self as unknown as WorkerScope,
  (self as unknown as { __WB_MANIFEST: PrecacheEntry[] }).__WB_MANIFEST,
)
