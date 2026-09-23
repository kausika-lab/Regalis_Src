import { lazy, Suspense, type ComponentType } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { Spinner } from '@/components/ui';
import { AuthLayout } from '@/layouts/AuthLayout';
import { PortalLayout } from '@/layouts/PortalLayout';
import { LoginPage } from '@/pages/auth/LoginPage';
import { PermissionRoute } from './PermissionRoute';
import { PortalRoute } from './PortalRoute';
import { ProtectedRoute } from './ProtectedRoute';

/** Day 11 hardening: every page is its own chunk — the first paint downloads the
 * login screen, not the whole application. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
function lazyPage<K extends string, T extends Record<K, ComponentType<any>>>(
  loader: () => Promise<T>,
  name: K,
) {
  return lazy(async () => ({ default: (await loader())[name] }));
}

// Auth (login stays eager — it is the first paint)
const RegisterPage = lazyPage(() => import('@/pages/auth/RegisterPage'), 'RegisterPage');
const VerifyOtpPage = lazyPage(() => import('@/pages/auth/VerifyOtpPage'), 'VerifyOtpPage');
const ForgotPasswordPage = lazyPage(() => import('@/pages/auth/ForgotPasswordPage'), 'ForgotPasswordPage');
const ResetPasswordPage = lazyPage(() => import('@/pages/auth/ResetPasswordPage'), 'ResetPasswordPage');

// Customer portal
const CustomerHomePage = lazyPage(() => import('@/pages/customer/CustomerHomePage'), 'CustomerHomePage');
const NotificationsPage = lazyPage(() => import('@/pages/customer/NotificationsPage'), 'NotificationsPage');
const BookWorkspacePage = lazyPage(() => import('@/pages/customer/BookWorkspacePage'), 'BookWorkspacePage');
const MyBookingsPage = lazyPage(() => import('@/pages/customer/MyBookingsPage'), 'MyBookingsPage');
const WalletPage = lazyPage(() => import('@/pages/customer/WalletPage'), 'WalletPage');
const InvoicesPage = lazyPage(() => import('@/pages/customer/InvoicesPage'), 'InvoicesPage');
const MyQuotationsPage = lazyPage(() => import('@/pages/customer/QuotationsPage'), 'MyQuotationsPage');
const MyQuotationDetailPage = lazyPage(() => import('@/pages/customer/QuotationsPage'), 'MyQuotationDetailPage');
const PassPlansPage = lazyPage(() => import('@/pages/customer/PassesPage'), 'PassPlansPage');
const MyQrPage = lazyPage(() => import('@/pages/customer/MyQrPage'), 'MyQrPage');
const WorkStatusPage = lazyPage(() => import('@/pages/customer/WorkStatusPage'), 'WorkStatusPage');
const ServicesPage = lazyPage(() => import('@/pages/customer/ServicesPage'), 'ServicesPage');
const MyTicketsPage = lazyPage(() => import('@/pages/customer/SupportPages'), 'MyTicketsPage');
const RaiseTicketPage = lazyPage(() => import('@/pages/customer/SupportPages'), 'RaiseTicketPage');
const TicketDetailPage = lazyPage(() => import('@/pages/customer/SupportPages'), 'TicketDetailPage');
const ContactUsPage = lazyPage(() => import('@/pages/customer/ContactUsPage'), 'ContactUsPage');
const ProfilePage = lazyPage(() => import('@/pages/ProfilePage'), 'ProfilePage');

// Admin portal
const AdminDashboardPage = lazyPage(() => import('@/pages/admin/AdminDashboardPage'), 'AdminDashboardPage');
const UsersPage = lazyPage(() => import('@/pages/admin/UsersPage'), 'UsersPage');
const AdminCustomersPage = lazyPage(() => import('@/pages/admin/AdminCustomersPages'), 'AdminCustomersPage');
const AdminCustomerDetailPage = lazyPage(() => import('@/pages/admin/AdminCustomersPages'), 'AdminCustomerDetailPage');
const RolesPage = lazyPage(() => import('@/pages/admin/RolesPage'), 'RolesPage');
const MenusPage = lazyPage(() => import('@/pages/admin/MenusPage'), 'MenusPage');
const LocationsPage = lazyPage(() => import('@/pages/admin/LocationsPage'), 'LocationsPage');
const WorkspacesPage = lazyPage(() => import('@/pages/admin/WorkspacesPage'), 'WorkspacesPage');
const AdminCalendarPage = lazyPage(() => import('@/pages/admin/AdminCalendarPage'), 'AdminCalendarPage');
const AdminBookingsPage = lazyPage(() => import('@/pages/admin/AdminBookingsPage'), 'AdminBookingsPage');
const AdminInvoicesPage = lazyPage(() => import('@/pages/admin/AdminFinancePages'), 'AdminInvoicesPage');
const AdminPaymentsPage = lazyPage(() => import('@/pages/admin/AdminFinancePages'), 'AdminPaymentsPage');
const AdminWalletPage = lazyPage(() => import('@/pages/admin/AdminFinancePages'), 'AdminWalletPage');
const AdminPassesPage = lazyPage(() => import('@/pages/admin/AdminFinancePages'), 'AdminPassesPage');
const AdminAccessScanPage = lazyPage(() => import('@/pages/admin/AdminAccessPages'), 'AdminAccessScanPage');
const AdminAccessLogsPage = lazyPage(() => import('@/pages/admin/AdminAccessPages'), 'AdminAccessLogsPage');
const AdminAttendancePage = lazyPage(() => import('@/pages/admin/AdminAccessPages'), 'AdminAttendancePage');
const AdminPayrollPage = lazyPage(() => import('@/pages/admin/AdminAccessPages'), 'AdminPayrollPage');
const AdminHousekeepingPage = lazyPage(() => import('@/pages/admin/AdminHousekeepingPage'), 'AdminHousekeepingPage');
const AdminInventoryItemsPage = lazyPage(() => import('@/pages/admin/AdminInventoryPages'), 'AdminInventoryItemsPage');
const AdminInventoryTransactionsPage = lazyPage(() => import('@/pages/admin/AdminInventoryPages'), 'AdminInventoryTransactionsPage');
const AdminIncomingGoodsPage = lazyPage(() => import('@/pages/admin/AdminInventoryGoodsPages'), 'AdminIncomingGoodsPage');
const AdminOutgoingGoodsPage = lazyPage(() => import('@/pages/admin/AdminInventoryGoodsPages'), 'AdminOutgoingGoodsPage');
const AdminCurrentStockPage = lazyPage(() => import('@/pages/admin/AdminInventoryGoodsPages'), 'AdminCurrentStockPage');
const AdminSuppliersPage = lazyPage(() => import('@/pages/admin/AdminSuppliersPage'), 'AdminSuppliersPage');
const AdminPurchaseOrdersPage = lazyPage(() => import('@/pages/admin/AdminPurchaseOrderPages'), 'AdminPurchaseOrdersPage');
const AdminPurchaseOrderDetailPage = lazyPage(() => import('@/pages/admin/AdminPurchaseOrderPages'), 'AdminPurchaseOrderDetailPage');
const AdminExpensesPage = lazyPage(() => import('@/pages/admin/AdminExpensesPage'), 'AdminExpensesPage');
const AdminQuotationsPage = lazyPage(() => import('@/pages/admin/AdminQuotationPages'), 'AdminQuotationsPage');
const AdminQuotationDetailPage = lazyPage(() => import('@/pages/admin/AdminQuotationPages'), 'AdminQuotationDetailPage');
const AdminBankAccountsPage = lazyPage(() => import('@/pages/admin/AdminBankPages'), 'AdminBankAccountsPage');
const AdminBankTransactionsPage = lazyPage(() => import('@/pages/admin/AdminBankPages'), 'AdminBankTransactionsPage');
const AdminBankReconciliationPage = lazyPage(() => import('@/pages/admin/AdminBankPages'), 'AdminBankReconciliationPage');
const AdminFinanceDashboardPage = lazyPage(() => import('@/pages/admin/AdminFinanceDashboardPage'), 'AdminFinanceDashboardPage');
const AdminServicesPage = lazyPage(() => import('@/pages/admin/AdminCataloguePages'), 'AdminServicesPage');
const AdminBannersPage = lazyPage(() => import('@/pages/admin/AdminCataloguePages'), 'AdminBannersPage');
const AdminSettingsPage = lazyPage(() => import('@/pages/admin/AdminCataloguePages'), 'AdminSettingsPage');
const AdminNotificationsPage = lazyPage(() => import('@/pages/admin/AdminNotificationsPage'), 'AdminNotificationsPage');
const AdminTicketsPage = lazyPage(() => import('@/pages/admin/AdminTicketsPage'), 'AdminTicketsPage');
const AdminTicketDetailPage = lazyPage(() => import('@/pages/admin/AdminTicketsPage'), 'AdminTicketDetailPage');
const AdminReportsPage = lazyPage(() => import('@/pages/admin/AdminReportsPages'), 'AdminReportsPage');
const ReportShellPage = lazyPage(() => import('@/pages/admin/AdminReportsPages'), 'ReportShellPage');
const AdminAuditPage = lazyPage(() => import('@/pages/admin/AdminAuditPage'), 'AdminAuditPage');

// Errors
const ForbiddenPage = lazyPage(() => import('@/pages/errors/ForbiddenPage'), 'ForbiddenPage');
const NotFoundPage = lazyPage(() => import('@/pages/errors/NotFoundPage'), 'NotFoundPage');

export function AppRoutes() {
  return (
    <Suspense fallback={<Spinner center />}>
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
          <Route element={<PortalRoute portal="customer" />}>
            <Route element={<PortalLayout title="Regalis WorkSpace" portal="customer" />}>
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
              <Route element={<PermissionRoute permission="QUOTATION.VIEW" />}>
                <Route path="/quotations" element={<MyQuotationsPage />} />
                <Route path="/quotations/:id" element={<MyQuotationDetailPage />} />
              </Route>
              <Route element={<PermissionRoute permission="INVOICE.VIEW" />}>
                <Route path="/invoices" element={<InvoicesPage />} />
              </Route>
              <Route element={<PermissionRoute permission="PASS.VIEW" />}>
                <Route path="/day-pass" element={<PassPlansPage passType={1} />} />
                <Route path="/monthly-pass" element={<PassPlansPage passType={2} />} />
                <Route path="/passes" element={<PassPlansPage />} />
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
              <Route element={<PermissionRoute permission="TICKET.VIEW" />}>
                <Route path="/support" element={<MyTicketsPage />} />
                <Route path="/support/tickets" element={<MyTicketsPage />} />
                <Route path="/support/tickets/:id" element={<TicketDetailPage />} />
              </Route>
              <Route element={<PermissionRoute permission="TICKET.CREATE" />}>
                <Route path="/support/raise" element={<RaiseTicketPage />} />
              </Route>
              <Route path="/contact" element={<ContactUsPage />} />
              <Route path="/profile" element={<ProfilePage />} />
            </Route>
          </Route>
        </Route>

        {/* Admin portal */}
        <Route element={<ProtectedRoute />}>
          <Route element={<PortalRoute portal="admin" />}>
            <Route element={<PortalLayout title="Administration" portal="admin" />}>
              <Route element={<PermissionRoute permission="DASHBOARD.VIEW" />}>
                <Route path="/admin" element={<AdminDashboardPage />} />
              </Route>
              <Route element={<PermissionRoute permission="USER.VIEW" />}>
                <Route path="/admin/users" element={<UsersPage />} />
              </Route>
              <Route element={<PermissionRoute permission="CUSTOMER.VIEW" />}>
                <Route path="/admin/customers" element={<AdminCustomersPage />} />
                <Route path="/admin/customers/:id" element={<AdminCustomerDetailPage />} />
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
                <Route path="/admin/inventory/incoming" element={<AdminIncomingGoodsPage />} />
                <Route path="/admin/inventory/outgoing" element={<AdminOutgoingGoodsPage />} />
                <Route path="/admin/inventory/stock" element={<AdminCurrentStockPage />} />
              </Route>
              <Route element={<PermissionRoute permission="SUPPLIER.VIEW" />}>
                <Route path="/admin/suppliers" element={<AdminSuppliersPage />} />
              </Route>
              <Route element={<PermissionRoute permission="PURCHASE.VIEW" />}>
                <Route path="/admin/purchase-orders" element={<AdminPurchaseOrdersPage />} />
                <Route path="/admin/purchase-orders/:id" element={<AdminPurchaseOrderDetailPage />} />
              </Route>
              <Route element={<PermissionRoute permission="EXPENSE.VIEW" />}>
                <Route path="/admin/expenses" element={<AdminExpensesPage />} />
              </Route>
              <Route element={<PermissionRoute permission="QUOTATION.VIEW" />}>
                <Route path="/admin/quotations" element={<AdminQuotationsPage />} />
                <Route path="/admin/quotations/:id" element={<AdminQuotationDetailPage />} />
              </Route>
              <Route element={<PermissionRoute permission="PAYMENT.VIEW" />}>
                <Route path="/admin/finance" element={<AdminFinanceDashboardPage />} />
              </Route>
              <Route element={<PermissionRoute permission="BANK.VIEW" />}>
                <Route path="/admin/bank/accounts" element={<AdminBankAccountsPage />} />
                <Route path="/admin/bank/transactions" element={<AdminBankTransactionsPage />} />
                <Route path="/admin/bank/reconciliation" element={<AdminBankReconciliationPage />} />
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
              <Route element={<PermissionRoute permission="NOTIFICATION.VIEW" />}>
                <Route path="/admin/notifications" element={<AdminNotificationsPage />} />
              </Route>
              <Route element={<PermissionRoute permission="TICKET.VIEW" />}>
                <Route path="/admin/tickets" element={<AdminTicketsPage />} />
                <Route path="/admin/tickets/:id" element={<AdminTicketDetailPage />} />
              </Route>
              <Route element={<PermissionRoute permission="REPORT.VIEW" />}>
                <Route path="/admin/reports" element={<AdminReportsPage />} />
                <Route path="/admin/reports/:key" element={<ReportShellPage />} />
              </Route>
              <Route element={<PermissionRoute permission="AUDIT.VIEW" />}>
                <Route path="/admin/audit" element={<AdminAuditPage />} />
              </Route>
              <Route path="/admin/profile" element={<ProfilePage />} />
            </Route>
          </Route>
        </Route>

        <Route path="/forbidden" element={<ForbiddenPage />} />
        <Route path="/404" element={<NotFoundPage />} />
        <Route path="*" element={<Navigate to="/404" replace />} />
      </Routes>
    </Suspense>
  );
}
