import { expect, test } from '@playwright/test';
import { ADMIN_EMAIL, adminPassword, login, visitAndCapture } from './helpers';

/**
 * Signs in on the Admin portal and opens every developed admin feature in the
 * browser, proving each page renders for a fully-permissioned user. A full-page
 * screenshot of every feature lands in e2e-results/screens/.
 */
const ADMIN_PAGES: Array<[path: string, name: string]> = [
  ['/admin', 'admin-dashboard'],
  ['/admin/users', 'admin-users'],
  ['/admin/customers', 'admin-customers'],
  ['/admin/roles', 'admin-roles'],
  ['/admin/menus', 'admin-menus'],
  ['/admin/locations', 'admin-locations'],
  ['/admin/workspaces', 'admin-workspaces'],
  ['/admin/workspaces/desks', 'admin-workspaces-desks'],
  ['/admin/workspaces/cabins', 'admin-workspaces-cabins'],
  ['/admin/workspaces/meeting-rooms', 'admin-workspaces-meeting-rooms'],
  ['/admin/calendar', 'admin-calendar'],
  ['/admin/bookings', 'admin-bookings'],
  ['/admin/invoices', 'admin-invoices'],
  ['/admin/payments', 'admin-payments'],
  ['/admin/wallet', 'admin-wallet'],
  ['/admin/passes/day', 'admin-passes-day'],
  ['/admin/passes/monthly', 'admin-passes-monthly'],
  ['/admin/access', 'admin-access-scan'],
  ['/admin/access/logs', 'admin-access-logs'],
  ['/admin/attendance', 'admin-attendance'],
  ['/admin/attendance/payroll', 'admin-payroll'],
  ['/admin/housekeeping', 'admin-housekeeping'],
  ['/admin/inventory/items', 'admin-inventory-items'],
  ['/admin/inventory/transactions', 'admin-inventory-transactions'],
  ['/admin/services', 'admin-services'],
  ['/admin/banners', 'admin-banners'],
  ['/admin/settings', 'admin-settings'],
  ['/admin/notifications', 'admin-notifications'],
  ['/admin/tickets', 'admin-tickets'],
  ['/admin/reports', 'admin-reports'],
  ['/admin/audit', 'admin-audit'],
  ['/admin/profile', 'admin-profile'],
];

test.describe('Admin portal — every feature renders', () => {
  test('sign in and walk all admin pages', async ({ page }) => {
    test.setTimeout(300_000);
    await login(page, 'admin', ADMIN_EMAIL, adminPassword());

    for (const [path, name] of ADMIN_PAGES) {
      await test.step(path, async () => {
        await visitAndCapture(page, path, name);
      });
    }
  });

  test('open the first report from the reports hub', async ({ page }) => {
    await login(page, 'admin', ADMIN_EMAIL, adminPassword());
    await page.goto('/admin/reports');

    const firstReport = page.locator('a[href^="/admin/reports/"]').first();
    await expect(firstReport, 'reports hub should list at least one report').toBeVisible({
      timeout: 15_000,
    });
    await firstReport.click();
    await page.waitForURL(/\/admin\/reports\/.+/, { timeout: 15_000 });
    await page.waitForTimeout(500);
    await page.screenshot({ path: 'e2e-results/screens/admin-report-shell.png', fullPage: true });
  });
});
