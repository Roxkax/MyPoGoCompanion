import { DetailScreen } from '../layout/Screen'
import { useTheme } from '../theme/themeContext'
import type { ThemeChoice } from '../theme/theme'
import { SegmentedSwitch } from '../ui/SegmentedSwitch'

const themeOptions = [
  { value: 'device', label: 'Follow device' },
  { value: 'light', label: 'Light' },
  { value: 'dark', label: 'Dark' },
] as const satisfies readonly { value: ThemeChoice; label: string }[]

/**
 * F2 Settings. For now only the Theme row (decision 2026-11-01); account, language, About & credits, install and
 * Admin join it with E09-F01. No sign-in needed: the theme is kept on this device.
 */
export function SettingsScreen() {
  const { choice, setChoice } = useTheme()
  return (
    <DetailScreen back="history" title="Settings">
      <div className="settings">
        <section className="settings-group" aria-labelledby="settings-theme">
          <h2 id="settings-theme" className="group-label">
            Theme
          </h2>
          <SegmentedSwitch
            label="Theme"
            options={themeOptions}
            value={choice}
            onChange={setChoice}
          />
          <p className="group-note">Saved on this device.</p>
        </section>
        <p className="fine-print">
          MyPoGoCompanion is an unofficial fan project, not affiliated with Niantic, Nintendo,
          Creatures, Game Freak or The Pokémon Company.
        </p>
      </div>
    </DetailScreen>
  )
}
