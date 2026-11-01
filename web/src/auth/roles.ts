import type { Role } from './authApi'

/** Admins and the super admin see Admin in the sidebar and profile menu and can open the admin screens. */
export function isAdmin(role: Role): boolean {
  return role === 'Admin' || role === 'SuperAdmin'
}
