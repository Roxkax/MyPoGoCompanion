import { Link } from 'react-router'
import { ApiStatus } from '../components/ApiStatus'
import { Icon } from '../components/icons'
import { TabScreen } from '../layout/Screen'
import { useIsDesktop } from '../layout/useMediaQuery'
import { ComingSoon } from './ComingSoon'
import { todayLabel } from './today'

/**
 * A1 Today, the home screen: the frame now (title, date, calendar button, search), the content with E06-F01. Until
 * then it also shows whether the app reaches the API (the smoke test and the UI tests look for it).
 */
export function TodayScreen() {
  const desktop = useIsDesktop()
  return (
    <TabScreen
      title="Today"
      subtitle={todayLabel(new Date(), desktop)}
      search
      actions={
        <Link to="/calendar" className="icon-button icon-button-soft" aria-label="Calendar">
          <Icon name="calendar" size={22} />
        </Link>
      }
    >
      <div className="stack">
        <ComingSoon
          screen="A1"
          name="Today"
          feature="E06-F01 Today (home)"
          text="What’s on now: raids, Max Battles, events, eggs and research."
        />
        <ApiStatus />
      </div>
    </TabScreen>
  )
}
