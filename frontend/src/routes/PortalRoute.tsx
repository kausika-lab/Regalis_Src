import { Navigate, Outlet } from 'react-router-dom';
import type { Portal } from '@/models/auth';
import { useAuthStore } from '@/store/authStore';

/** UserType 1 is a customer; 2 (admin) and 3 (staff) both sign in on the admin portal. */
export function portalOf(userType: number | undefined): Portal {
  return userType === 1 ? 'customer' : 'admin';
}

/** The home screen of a portal — where a signed-in user belongs by default. */
export function homeOf(portal: Portal): string {
  return portal === 'admin' ? '/admin' : '/';
}

/** True when the path lives in the admin portal's route block. */
export function isAdminPath(path: string): boolean {
  return path === '/admin' || path.startsWith('/admin/');
}

/**
 * Keeps each portal's route block to the users who signed in for it, e.g.
 * <Route element={<PortalRoute portal="admin" />}>.
 *
 * The API already decides this at login — it rejects credentials on the wrong
 * portal — but nothing stopped the SPA from rendering the other portal's shell
 * afterwards. Permissions alone are not that boundary: a customer holds
 * BOOKING.VIEW and INVOICE.VIEW, so PermissionRoute happily let them open
 * /admin/bookings. A user in the wrong portal is sent to their own home rather
 * than /forbidden — they are not denied a page, they are simply somewhere that
 * does not exist for them.
 */
export function PortalRoute({ portal }: { portal: Portal }) {
  const userType = useAuthStore((state) => state.user?.userType);
  const actual = portalOf(userType);
  return actual === portal ? <Outlet /> : <Navigate to={homeOf(actual)} replace />;
}
