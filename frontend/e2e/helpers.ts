import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { expect, type Page } from '@playwright/test';

export const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL ?? 'enquiry@amstag.in';

export function adminPassword(): string {
  const value = process.env.E2E_ADMIN_PASSWORD;
  if (!value) {
    throw new Error(
      'E2E_ADMIN_PASSWORD is not set. Run the suite via scripts\\run-e2e.ps1 ' +
        '(it reads Seed:AdminPassword from the API user-secrets), or set the ' +
        'variable yourself before `npx playwright test`.',
    );
  }
  return value;
}

/** Sign in on /login. Portal tab defaults to Customer; click the tab we need. */
export async function login(page: Page, portal: 'admin' | 'customer', email: string, password: string) {
  await page.goto('/login');
  await page.getByRole('tab', { name: portal === 'admin' ? 'Admin' : 'Customer' }).click();
  await page.getByPlaceholder('name@company.com').fill(email);
  await page.getByPlaceholder('Enter your password').fill(password);
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL(portal === 'admin' ? '**/admin' : (url) => url.pathname === '/', {
    timeout: 20_000,
  });
}

/**
 * The dev API (ConsoleOtpSender) logs every OTP: "DEV OTP for <dest> (<purpose>): <code>".
 * Serilog also writes it to backend/src/Regalis.API/logs/regalis-api-<yyyyMMdd>.log,
 * so the code is retrievable no matter who started the API (run-e2e.ps1 or VS F5).
 */
export async function readLatestOtp(email: string, purpose = 'Registration'): Promise<string> {
  // Tests run with cwd = frontend/ (playwright.config.ts lives there).
  const logDir =
    process.env.E2E_API_LOG_DIR ?? resolve(process.cwd(), '../backend/src/Regalis.API/logs');
  const stamp = new Date().toISOString().slice(0, 10).replace(/-/g, '');
  const logFile = resolve(logDir, `regalis-api-${stamp}.log`);
  // Serilog's file template renders the purpose in quotes: ("Registration").
  const pattern = new RegExp(
    `DEV OTP for ${email.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')} \\("?${purpose}"?\\): (\\d{4,8})`,
    'gi',
  );

  for (let attempt = 0; attempt < 30; attempt++) {
    if (existsSync(logFile)) {
      const matches = [...readFileSync(logFile, 'utf8').matchAll(pattern)];
      const last = matches.at(-1);
      if (last) {
        return last[1];
      }
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  throw new Error(
    `No OTP for ${email} found in ${logFile} after 30s. ` +
      'Is the API running with the Console OTP sender (Development environment)?',
  );
}

/** Visit a route and prove the feature page actually rendered (no redirect, no error page). */
export async function visitAndCapture(page: Page, path: string, screenshotName: string) {
  await page.goto(path);
  // Not networkidle: pages that poll (dashboard, notifications) never settle.
  await page.waitForLoadState('domcontentloaded');
  await page.waitForTimeout(500);
  // PermissionRoute redirects to /forbidden and unknown paths land on /404 —
  // either one means the feature did NOT render for this signed-in user.
  expect(page.url(), `expected to stay on ${path}`).not.toMatch(/\/(forbidden|404)$/);
  // Generous timeout: lazy page chunks can take a while on a loaded dev machine.
  await expect(page.locator('main, .portal, [role="main"]').first()).toBeVisible({
    timeout: 15_000,
  });
  await page.screenshot({
    path: `e2e-results/screens/${screenshotName}.png`,
    fullPage: true,
  });
}
