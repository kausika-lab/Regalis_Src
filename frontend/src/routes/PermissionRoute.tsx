import { Navigate, Outlet } from 'react-router-dom';
import { usePermission } from '@/hooks/usePermission';

/**
 * Guards a route behind a permission code, e.g.
 * <Route element={<PermissionRoute permission="BOOKING.VIEW" />}>.
 * Convenience only — the API is the enforcement point.
 */
export function PermissionRoute({ permission }: { permission: string }) {
  const allowed = usePermission(permission);
  return allowed ? <Outlet /> : <Navigate to="/forbidden" replace />;
}
