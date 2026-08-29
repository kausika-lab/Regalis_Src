import { Navigate, Route, Routes } from 'react-router-dom';
import { AuthLayout } from '@/layouts/AuthLayout';
import { PortalLayout } from '@/layouts/PortalLayout';
import {
  AdminAccessLogsPage,
  AdminAccessScanPage,
  AdminAttendancePage,
  AdminPayrollPage,
} from '@/pages/admin/AdminAccessPages';
import { AdminBookingsPage } from '@/pages/admin/AdminBookingsPage';
import { AdminCalendarPage } from '@/pages/admin/AdminCalendarPage';
import { AdminDashboardPage } from '@/pages/admin/AdminDashboardPage';
import {
  AdminBannersPage,
  AdminServicesPage,
  AdminSettingsPage,
} from '@/pages/admin/AdminCataloguePages';
import {
  AdminInvoicesPage,
  AdminPassesPage,
  AdminPaymentsPage,
  AdminWalletPage,
} from '@/pages/admin/AdminFinancePages';
import { AdminHousekeepingPage } from '@/pages/admin/AdminHousekeepingPage';
import {
  AdminInventoryItemsPage,
  AdminInventoryTransactionsPage,
} from '@/pages/admin/AdminInventoryPages';
import { LocationsPage } from '@/pages/admin/LocationsPage';
import { MenusPage } from '@/pages/admin/MenusPage';
import { RolesPage } from '@/pages/admin/RolesPage';
import { UsersPage } from '@/pages/admin/UsersPage';
import { WorkspacesPage } from '@/pages/admin/WorkspacesPage';
import { ForgotPasswordPage } from '@/pages/auth/ForgotPasswordPage';
import { LoginPage } from '@/pages/auth/LoginPage';
import { RegisterPage } from '@/pages/auth/RegisterPage';
import { ResetPasswordPage } from '@/pages/auth/ResetPasswordPage';
import { VerifyOtpPage } from '@/pages/auth/VerifyOtpPage';
import { BookWorkspacePage } from '@/pages/customer/BookWorkspacePage';
import { ContactUsPage } from '@/pages/customer/ContactUsPage';
import { CustomerHomePage } from '@/pages/customer/CustomerHomePage';
import { InvoicesPage } from '@/pages/customer/InvoicesPage';
import { MyBookingsPage } from '@/pages/customer/MyBookingsPage';
import { NotificationsPage } from '@/pages/customer/NotificationsPage';
import { MyPassesPage, PassPlansPage } from '@/pages/customer/PassesPage';
import { MyQrPage } from '@/pages/customer/MyQrPage';
import { ServicesPage } from '@/pages/customer/ServicesPage';
import { WalletPage } from '@/pages/customer/WalletPage';
import { WorkStatusPage } from '@/pages/customer/WorkStatusPage';
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
          <Route path="/notifications" element={<NotificationsPage />} />
          <Route element={<PermissionRoute permission="BOOKING.CREATE" />}>
            <Route path="/book" element={<BookWorkspacePage />} />
          </Route>
          <Route element={<PermissionRoute permission="BOOKING.VIEW" />}>
            <Route path="/bookings" element={<MyBookingsPage />} />
          </Route>
          <Route element={<PermissionRoute permission="WALLET.VIEW" />}>
            <Route path="/wallet" element={<WalletPage />} />
          </Route>
          <Route element={<PermissionRoute permission="INVOICE.VIEW" />}>
            <Route path="/invoices" element={<InvoicesPage />} />
          </Route>
          <Route element={<PermissionRoute permission="PASS.VIEW" />}>
            <Route path="/day-pass" element={<PassPlansPage passType={1} />} />
            <Route path="/monthly-pass" element={<PassPlansPage passType={2} />} />
            <Route path="/passes" element={<MyPassesPage />} />
          </Route>
          <Route element={<PermissionRoute permission="ACCESS.VIEW" />}>
            <Route path="/qr" element={<MyQrPage />} />
          </Route>
          <Route element={<PermissionRoute permission="ATTENDANCE.VIEW" />}>
            <Route path="/work-status" element={<WorkStatusPage />} />
          </Route>
          <Route element={<PermissionRoute permission="SERVICE.VIEW" />}>
            <Route path="/services" element={<ServicesPage />} />
          </Route>
          <Route path="/contact" element={<ContactUsPage />} />
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
          <Route element={<PermissionRoute permission="LOCATION.VIEW" />}>
            <Route path="/admin/locations" element={<LocationsPage />} />
          </Route>
          <Route element={<PermissionRoute permission="WORKSPACE.VIEW" />}>
            <Route path="/admin/workspaces" element={<WorkspacesPage />} />
            <Route path="/admin/workspaces/desks" element={<WorkspacesPage typeCode="DESK" />} />
            <Route path="/admin/workspaces/cabins" element={<WorkspacesPage typeCode="CABIN" />} />
            <Route
              path="/admin/workspaces/meeting-rooms"
              element={<WorkspacesPage typeCode="MEETING_ROOM" />}
            />
          </Route>
          <Route element={<PermissionRoute permission="CALENDAR.VIEW" />}>
            <Route path="/admin/calendar" element={<AdminCalendarPage />} />
          </Route>
          <Route element={<PermissionRoute permission="BOOKING.VIEW" />}>
            <Route path="/admin/bookings" element={<AdminBookingsPage />} />
          </Route>
          <Route element={<PermissionRoute permission="INVOICE.VIEW" />}>
            <Route path="/admin/invoices" element={<AdminInvoicesPage />} />
          </Route>
          <Route element={<PermissionRoute permission="PAYMENT.VIEW" />}>
            <Route path="/admin/payments" element={<AdminPaymentsPage />} />
          </Route>
          <Route element={<PermissionRoute permission="WALLET.VIEW" />}>
            <Route path="/admin/wallet" element={<AdminWalletPage />} />
          </Route>
          <Route element={<PermissionRoute permission="PASS.VIEW" />}>
            <Route path="/admin/passes/day" element={<AdminPassesPage passType={1} />} />
            <Route path="/admin/passes/monthly" element={<AdminPassesPage passType={2} />} />
          </Route>
          <Route element={<PermissionRoute permission="ACCESS.CREATE" />}>
            <Route path="/admin/access" element={<AdminAccessScanPage />} />
          </Route>
          <Route element={<PermissionRoute permission="ACCESS.VIEW" />}>
            <Route path="/admin/access/logs" element={<AdminAccessLogsPage />} />
          </Route>
          <Route element={<PermissionRoute permission="ATTENDANCE.VIEW" />}>
            <Route path="/admin/attendance" element={<AdminAttendancePage />} />
          </Route>
          <Route element={<PermissionRoute permission="PAYROLL.EXPORT" />}>
            <Route path="/admin/attendance/payroll" element={<AdminPayrollPage />} />
          </Route>
          <Route element={<PermissionRoute permission="HOUSEKEEPING.VIEW" />}>
            <Route path="/admin/housekeeping" element={<AdminHousekeepingPage />} />
          </Route>
          <Route element={<PermissionRoute permission="INVENTORY.VIEW" />}>
            <Route path="/admin/inventory/items" element={<AdminInventoryItemsPage />} />
            <Route path="/admin/inventory/transactions" element={<AdminInventoryTransactionsPage />} />
          </Route>
          <Route element={<PermissionRoute permission="SERVICE.VIEW" />}>
            <Route path="/admin/services" element={<AdminServicesPage />} />
          </Route>
          <Route element={<PermissionRoute permission="BANNER.VIEW" />}>
            <Route path="/admin/banners" element={<AdminBannersPage />} />
          </Route>
          <Route element={<PermissionRoute permission="SETTING.VIEW" />}>
            <Route path="/admin/settings" element={<AdminSettingsPage />} />
          </Route>
        </Route>
      </Route>

      <Route path="/forbidden" element={<ForbiddenPage />} />
      <Route path="/404" element={<NotFoundPage />} />
      <Route path="*" element={<Navigate to="/404" replace />} />
    </Routes>
  );
}
