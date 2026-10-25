# Reports & Analysis — staged 24 Oct 2026

Built from `Regalis_WorkSpace_Reports_Analysis_Feature_Specification (1).docx`.
API source: `C:\Users\Administrator\Downloads\Regalis-live`, branch `feature/reports-analysis`.
Deploy: `deploy-reports-analysis.cmd` (elevated). The frontend installs only after the API verifies.

## How the spec's open items were answered (change any of them later)

| # | Open item | What was built |
|---|-----------|----------------|
| 1 | Revenue = cash or invoiced? | Money received, net of refunds, in IST. Wallet top-ups are not revenue; the wallet counts when spent (read from the wallet debit, so legacy rows without a Payment count too). Invoiced value appears only in the GST summary and invoice register, labelled "invoiced". |
| 2 | Financial year | 1 April – 31 March; FY preset on the overview. |
| 3 | Occupancy basis | Booked time, not check-ins. Unit is the space-hour (a booking takes the whole space), so one 16-seat room does not outweigh 14 desks. Blocked time is removed from available hours. |
| 4 | Wallet top-ups | Not revenue (see 1). |
| 5 | Custom report builder | Button present; opens a "later phase" note. |
| 6 | Role mapping | ADMIN: all. FINANCE: view, export, finance. MANAGER and SUPPORT: unchanged (view + export). Edit in Roles & Permissions. |
| 7 | Opening hours | Each location's own working hours and days (RR Nagar: 08:00–21:00, Mon–Sat). |
| 8 | Large exports | Over 5,000 rows → built in the background, emailed link valid 7 days (sign-in required). |
| 9 | Nightly rollup | 02:00 IST; rebuilds the last two days and fills any missing day back to the first activity. |
| 10 | Location restriction | None — all locations, with a filter (hidden while there is one location). |

## Departures from the spec, and why

- **24 reports, not 28.** The spec's catalogue lists 24; all are built.
- **Data Regalis does not record** — event add-ons/headcount, booking source "website vs app vs walk-in",
  Wi-Fi sessions, credit notes, KYC, asset-linked maintenance: those reports show what exists and say so
  (e.g. refunds go to wallets; "Profile (KYC)" = onboarding complete).
- **Multi-day revenue spreading (§8.5)** conflicts with "revenue is cash on the day received" (§8.2);
  the cash rule wins.
- **Scheduler ticks every minute**, not 15, so sends are on time and retries are 10 minutes apart.
- **Old reports not in the new catalogue** (expenses, bank statement, quotations, QR access, services orders,
  support tickets, inventory, goods in/out, finance summary) are no longer linked from the UI. Their old
  API (`/api/reports/*`) still works; say if any should be added to the library.

## Tests

- Unit: `ReportMathTests` (13) — comparisons, shares to 100, periods, FY, Indian money, IST schedules.
- Integration: `ReportsAnalysisTests` (7) — totals across pages, export = screen, revenue net of refunds,
  overview = reports, rollup re-runnable, staff-only, every report runs.
- Browser: Playwright against the deployed SPA + this API on a copy of UAT — 48 checks, no page errors.
