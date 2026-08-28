import { Navigate, Route, Routes } from 'react-router-dom';
import { AuthLayout } from '@/layouts/AuthLayout';
import { PortalLayout } from '@/layouts/PortalLayout';
import { AdminDashboardPage } from '@/pages/admin/AdminDashboardPage';
import { LoginPage } from '@/pages/auth/LoginPage';
import { CustomerHomePage } from '@/pages/customer/CustomerHomePage';
import { ForbiddenPage } from '@/pages/errors/ForbiddenPage';
import { NotFoundPage } from '@/pages/errors/NotFoundPage';
import { PermissionRoute } from './PermissionRoute';
import { ProtectedRoute } from './ProtectedRoute';

export function AppRoutes() {
  return (
    <Routes>
      {/* Public auth screens */}
      <Route element={<AuthLayout />}>
        <Route path="/login" element={<LoginPage />} />
      </Route>

      {/* Customer portal */}
      <Route element={<ProtectedRoute />}>
        <Route element={<PortalLayout title="Regalis WorkSpace" />}>
          <Route path="/" element={<CustomerHomePage />} />
        </Route>
      </Route>

      {/* Admin portal */}
      <Route element={<ProtectedRoute />}>
        <Route element={<PermissionRoute permission="DASHBOARD.VIEW" />}>
          <Route element={<PortalLayout title="Administration" />}>
            <Route path="/admin" element={<AdminDashboardPage />} />
          </Route>
        </Route>
      </Route>

      <Route path="/forbidden" element={<ForbiddenPage />} />
      <Route path="/404" element={<NotFoundPage />} />
      <Route path="*" element={<Navigate to="/404" replace />} />
    </Routes>
  );
}
