import type { ReactNode } from 'react'
import { PwaContext, type Pwa } from './pwaContext'

export function PwaProvider({ pwa, children }: { pwa: Pwa; children: ReactNode }) {
  return <PwaContext.Provider value={pwa}>{children}</PwaContext.Provider>
}
