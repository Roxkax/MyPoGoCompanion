import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { ApiStatus } from '../components/ApiStatus'
import { useFormats } from '../i18n/languageContext'
import { Icon } from '../components/icons'
import { TabScreen } from '../layout/Screen'
import { useIsDesktop } from '../layout/useMediaQuery'
import { ComingSoon } from './ComingSoon'

/**
 * A1 Today, the home screen: the frame now (title, date, calendar button, search), the content with E06-F01. Until
 * then it also shows whether the app reaches the API (the smoke test and the UI tests look for it).
 */
export function TodayScreen() {
  const desktop = useIsDesktop()
  const { t } = useTranslation()
  const formats = useFormats()
  return (
    <TabScreen
      title="today"
      subtitle={formats.longDay(new Date(), desktop)}
      search
      actions={
        <Link
          to="/calendar"
          className="icon-button icon-button-soft"
          aria-label={t('nav.calendar')}
        >
          <Icon name="calendar" size={22} />
        </Link>
      }
    >
      <div className="stack">
        <ComingSoon id="today" screen="A1" feature="E06-F01 Today (home)" />
        <ApiStatus />
      </div>
    </TabScreen>
  )
}
