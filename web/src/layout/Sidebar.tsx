import { Link, useLocation } from 'react-router'
import { isAdmin } from '../auth/roles'
import { useAuth } from '../auth/authContext'
import { Icon } from '../components/icons'
import type { AppEnvironment } from '../environment'
import { activeSidebarItem, quickLinks, tabs, type NavItem, type SidebarKey } from './nav'

function SidebarLink({ item, active }: { item: NavItem; active: SidebarKey | null }) {
  const on = item.key === active
  return (
    <Link to={item.to} className="sidebar-item" aria-current={on ? 'page' : undefined}>
      <Icon name={item.icon} size={20} />
      {item.label}
    </Link>
  )
}

/** The desktop sidebar: logo, the five tabs, quick links, then Settings and (for admins) Admin at the bottom. */
export function Sidebar({ environment }: { environment: AppEnvironment }) {
  const { pathname } = useLocation()
  const { state } = useAuth()
  const active = activeSidebarItem(pathname)
  const admin = state.status === 'signedIn' && isAdmin(state.user.role)

  return (
    <aside className="sidebar">
      <Link to="/" className="sidebar-brand" aria-label="MyPoGoCompanion, Today">
        <img src={environment.icon} alt="" width="30" height="30" />
        <span className="sidebar-brand-name">
          MyPoGoCompanion
          {environment.label && (
            <span className="env-pill" data-env={environment.kind}>
              {environment.label}
            </span>
          )}
        </span>
      </Link>
      <nav className="sidebar-nav" aria-label="Main">
        {tabs.map((tab) => (
          <SidebarLink key={tab.key} item={tab} active={active} />
        ))}
        <p className="sidebar-label" id="quick-links">
          Quick links
        </p>
        <div role="group" aria-labelledby="quick-links">
          {quickLinks.map((link) => (
            <SidebarLink key={link.key} item={link} active={active} />
          ))}
        </div>
      </nav>
      <nav className="sidebar-end" aria-label="Settings">
        <SidebarLink
          item={{ key: 'settings', label: 'Settings', icon: 'settings', to: '/settings' }}
          active={active}
        />
        {admin && (
          <SidebarLink
            item={{ key: 'admin', label: 'Admin', icon: 'admin', to: '/admin' }}
            active={active}
          />
        )}
      </nav>
    </aside>
  )
}
