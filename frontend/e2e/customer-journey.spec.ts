import { expect, test } from '@playwright/test';
import { login, readLatestOtp, visitAndCapture } from './helpers';

/**
 * The full customer lifecycle, end to end in the browser: register a brand-new
 * account, verify it with the real OTP (read from the dev API log), sign in,
 * and open every customer-facing feature.
 */
const stamp = Date.now();
const CUSTOMER = {
  name: 'E2E Walkthrough',
  email: `e2e.${stamp}@example.com`,
  mobile: `98${String(stamp).slice(-8)}`,
  password: 'E2e!Walkthrough1',
};

const CUSTOMER_PAGES: Array<[path: string, name: string]> = [
  ['/', 'customer-home'],
  ['/book', 'customer-book'],
  ['/bookings', 'customer-bookings'],
  ['/wallet', 'customer-wallet'],
  ['/invoices', 'customer-invoices'],
  ['/day-pass', 'customer-day-pass'],
  ['/monthly-pass', 'customer-monthly-pass'],
  ['/passes', 'customer-my-passes'],
  ['/qr', 'customer-qr'],
  ['/work-status', 'customer-work-status'],
  ['/services', 'customer-services'],
  ['/support', 'customer-support'],
  ['/support/raise', 'customer-raise-ticket'],
  ['/notifications', 'customer-notifications'],
  ['/contact', 'customer-contact'],
  ['/profile', 'customer-profile'],
];

test.describe.serial('Customer journey — register, verify, sign in, walk all features', () => {
  test('register a new account and verify it with the real OTP', async ({ page }) => {
    await page.goto('/register');
    await page.getByPlaceholder('Enter full name').fill(CUSTOMER.name);
    await page.getByPlaceholder('Enter email address').fill(CUSTOMER.email);
    await page.getByPlaceholder('Enter phone number').fill(CUSTOMER.mobile);
    await page.getByPlaceholder('Create a password').fill(CUSTOMER.password);
    await page.getByPlaceholder('Re-enter the password').fill(CUSTOMER.password);
    await page.getByRole('button', { name: 'Create Account' }).click();
    await page.waitForURL('**/verify-otp', { timeout: 20_000 });

    const code = await readLatestOtp(CUSTOMER.email);
    await page.getByPlaceholder('6-digit code').fill(code);
    await page.getByRole('button', { name: 'Verify', exact: true }).click();
    await page.waitForURL('**/login', { timeout: 20_000 });
  });

  test('sign in and walk every customer feature', async ({ page }) => {
    test.setTimeout(300_000);
    await login(page, 'customer', CUSTOMER.email, CUSTOMER.password);

    for (const [path, name] of CUSTOMER_PAGES) {
      await test.step(path, async () => {
        await visitAndCapture(page, path, name);
      });
    }
  });
});
