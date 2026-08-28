import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/store/authStore';

/** Redirects unauthenticated visitors to /login, preserving the intended target. */
export function ProtectedRoute() {
  const isAuthenticated = useAuthStore((state) => state.accessToken !== null);
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }
  return <Outlet />;
}
