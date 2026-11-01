import { useTranslation } from 'react-i18next'
import { useLanguageChoice } from '../i18n/useLanguageChoice'
import { languageNames, languages } from '../i18n/languages'
import { DetailScreen } from '../layout/Screen'
import { useTheme } from '../theme/themeContext'
import type { ThemeChoice } from '../theme/theme'
import { ChoiceRows } from '../ui/ChoiceRows'
import { SegmentedSwitch } from '../ui/SegmentedSwitch'

const themeOptions = [
  { value: 'device', label: 'settings.themeDevice' },
  { value: 'light', label: 'settings.themeLight' },
  { value: 'dark', label: 'settings.themeDark' },
] as const satisfies readonly { value: ThemeChoice; label: string }[]

/** Each language's row shows its name in that language, so anyone can find their own. */
const languageOptions = languages.map((language) => ({
  value: language,
  label: languageNames[language],
  lang: language,
}))

/**
 * F2 Settings: Language and Theme for now; account, About & credits, install and Admin join them with E09-F01.
 * No sign-in needed: both are kept on this device, and signed in the language is also saved with the account
 * (decision 2026-11-01).
 */
export function SettingsScreen() {
  const { t } = useTranslation()
  const { choice, setChoice } = useTheme()
  const { language, choose, notSaved } = useLanguageChoice()
  return (
    <DetailScreen back="history" title="settings">
      <div className="settings">
        <section className="settings-group" aria-labelledby="settings-language">
          <h2 id="settings-language" className="group-label">
            {t('settings.language')}
          </h2>
          <ChoiceRows
            label={t('settings.language')}
            options={languageOptions}
            value={language}
            onChange={(next) => void choose(next)}
          />
          <p className="group-note">{t('settings.languageNote')}</p>
          {notSaved && (
            <p className="group-note settings-warning" role="alert">
              {t('settings.languageNotSaved')}
            </p>
          )}
        </section>
        <section className="settings-group" aria-labelledby="settings-theme">
          <h2 id="settings-theme" className="group-label">
            {t('settings.theme')}
          </h2>
          <SegmentedSwitch
            label={t('settings.theme')}
            options={themeOptions.map((option) => ({ ...option, label: t(option.label) }))}
            value={choice}
            onChange={setChoice}
          />
          <p className="group-note">{t('settings.themeNote')}</p>
        </section>
        <p className="fine-print">{t('settings.disclaimer')}</p>
      </div>
    </DetailScreen>
  )
}
