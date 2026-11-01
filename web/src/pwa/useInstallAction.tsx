import { useCallback, useState, type ReactNode } from 'react'
import { InstallGuide } from './InstallGuide'
import { useInstall } from './pwaContext'
import type { InstallKind, InstallOutcome } from './install'

/**
 * "Install" for the Settings row and the hint: the browser's own install window where there is one, otherwise the
 * guide. `guide` is the guide window to draw while it's open.
 */
export function useInstallAction(): {
  kind: InstallKind
  install: () => Promise<InstallOutcome | 'guide'>
  guide: ReactNode
} {
  const { kind, prompt } = useInstall()
  const [guideKind, setGuideKind] = useState<'ios' | 'menu' | null>(null)
  const close = useCallback(() => setGuideKind(null), [])

  const install = useCallback(async () => {
    if (kind === 'prompt') {
      return prompt()
    }
    if (kind === 'ios' || kind === 'menu') {
      setGuideKind(kind)
    }
    return 'guide' as const
  }, [kind, prompt])

  return {
    kind,
    install,
    guide: guideKind && <InstallGuide kind={guideKind} onClose={close} />,
  }
}
