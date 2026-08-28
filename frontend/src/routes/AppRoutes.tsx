import { Navigate, Route, Routes } from 'react-router-dom';
import { AuthLayout } from '@/layouts/AuthLayout';
import { PortalLayout } from '@/layouts/PortalLayout';
import { AdminDashboardPage } from '@/pages/admin/AdminDashboardPage';
import { MenusPage } from '@/pages/admin/MenusPage';
import { RolesPage } from '@/pages/admin/RolesPage';
import { UsersPage } from '@/pages/admin/UsersPage';
import { ForgotPasswordPage } from '@/pages/auth/ForgotPasswordPage';
import { LoginPage } from '@/pages/auth/LoginPage';
import { RegisterPage } from '@/pages/auth/RegisterPage';
import { ResetPasswordPage } from '@/pages/auth/ResetPasswordPage';
import { VerifyOtpPage } from '@/pages/auth/VerifyOtpPage';
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
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/verify-otp" element={<VerifyOtpPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
      </Route>

      {/* Customer portal */}
      <Route element={<ProtectedRoute />}>
        <Route element={<PortalLayout title="Regalis WorkSpace" />}>
          <Route path="/" element={<CustomerHomePage />} />
        </Route>
      </Route>

      {/* Admin portal */}
      <Route element={<ProtectedRoute />}>
        <Route element={<PortalLayout title="Administration" />}>
          <Route element={<PermissionRoute permission="DASHBOARD.VIEW" />}>
            <Route path="/admin" element={<AdminDashboardPage />} />
          </Route>
          <Route element={<PermissionRoute permission="USER.VIEW" />}>
            <Route path="/admin/users" element={<UsersPage />} />
          </Route>
          <Route element={<PermissionRoute permission="ROLE.VIEW" />}>
            <Route path="/admin/roles" element={<RolesPage />} />
          </Route>
          <Route element={<PermissionRoute permission="MENU.VIEW" />}>
            <Route path="/admin/menus" element={<MenusPage />} />
          </Route>
        </Route>
      </Route>

      <Route path="/forbidden" element={<ForbiddenPage />} />
      <Route path="/404" element={<NotFoundPage />} />
      <Route path="*" element={<Navigate to="/404" replace />} />
    </Routes>
  );
}
