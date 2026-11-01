import { ProfileButton } from '../components/ProfileButton'
import { AskButton } from './AskButton'
import { SearchButton } from './SearchButton'

/** The desktop top bar: search, Ask and the profile button. */
export function TopBar() {
  return (
    <header className="top-bar">
      <SearchButton wide />
      <span className="top-bar-end">
        <AskButton />
        <ProfileButton />
      </span>
    </header>
  )
}
