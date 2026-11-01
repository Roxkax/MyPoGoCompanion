import { Link, useLocation } from 'react-router'
import { Icon } from '../components/icons'
import { activeTab, tabs } from './nav'

/** The phone's bottom tab bar: Today, Pokédex, Battle, Filters, Collection. */
export function TabBar() {
  const { pathname } = useLocation()
  const active = activeTab(pathname)
  return (
    <nav className="tab-bar" aria-label="Tabs">
      {tabs.map((tab) => {
        const on = tab.key === active
        return (
          <Link
            key={tab.key}
            to={tab.to}
            className="tab-bar-item"
            aria-current={on ? 'page' : undefined}
          >
            <Icon name={tab.icon} size={24} strokeWidth={on ? 2.3 : 1.9} />
            {tab.label}
          </Link>
        )
      })}
    </nav>
  )
}
