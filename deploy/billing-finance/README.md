# Billing & Finance build — deploy kit

Implements `Regalis_WorkSpace_Billing_Finance_Build_Document.docx` (Oct 2026) against the
current API and SPA. This repo is the deploy snapshot; the API is built from
`C:\Users\Administrator\Downloads\Regalis-live\backend` on the UAT server, so the backend part
of this kit has to be copied into that source first. Everything here was compiled against the
current API (decompiled from `api\*.dll`), run against a copy of the Dev database, and tested
end to end (see **What was tested**).

| Part | Where | How it goes in |
|---|---|---|
| New backend files | `backend/src/...` (same layout as the source tree) | copy into `Regalis-live\backend\src\` |
| Edit to one existing file | `DependencyInjection.cs`, 3 lines; exact change in `backend/existing-files.diff` | apply by hand |
| EF migration | `backend/src/Regalis.Infrastructure/Persistence/Migrations/20261101165945_BillingFinance*.cs` | copy with the rest; see **Migration** |
| Database scripts | `db/migrations/20261101165945_BillingFinance.sql`, `20261101170000_BillingFinanceAccess.sql` (+ `_Rollback`) | run by `deploy-billing-finance.cmd` |
| Front end | `frontend/apply.mjs` + `frontend/assets/*.js` (`make-expenses-ex3.py` rebuilt EX3 from EX2) | run by `deploy-billing-finance.cmd` |
| Deploy | `deploy-billing-finance.cmd` (repo root), `verify.ps1` | run elevated on UAT |
| Tests | `tests/e2e_billing.py` (71 API checks), `tests/ui_tour.py`, `tests/ui_dialogs.py` (headless Chrome) | optional, against a test API; set `REGALIS_TEST_PASSWORD` (and `REGALIS_TEST_EMAIL`, default `admin@regalis.in`) to the test admin's sign-in first |

## What the build document asked for, and where it is

| Doc § | What | Screen / endpoint |
|---|---|---|
| 1.2 | Reconciliation menu row restored | `BillingFinanceAccess.sql` 3a; sidebar BANK band |
| 1.2, 11.6 | Company GSTIN / address / phone empty | **data, not code**: fill `Company.Gstin`, `Company.Address`, `Company.Phone` in Settings. Invoices and GST pages now warn while they are empty |
| 2 | Five sidebar bands | menu rows with no route (icon `Band`), drawn as labels; empty bands hidden |
| 3 | Record a customer payment | Invoices list + invoice page `+ Record payment`; `POST /api/invoices/{id}/payments` (no gateway) |
| 3.6 | Payments page: every mode, source filter, receipts, reverse | `/admin/billing/payments`; `GET /api/payments/register`, `POST /api/payments/{id}/reverse` |
| 4, 5 | Purchase bills, bill detail (5 tabs), duplicate check, GST split with its reason | `/admin/billing/purchase-bills`, `/purchase-bills/{new,:id,:id/edit}`; PO detail `Create bill` |
| 6 | Vendor payments: one payment, several bills, less TDS, advances | `/admin/billing/vendor-payments` |
| 7 | Ledger / outstanding / ageing for customers and suppliers, statement PDF/Excel/email | `/admin/billing/ledger/:type/:id`, `/admin/billing/ledgers`; `Ledger` on Suppliers and Customers |
| 8 | Statement import (CSV/XLSX, remembered mapping, preview, duplicates, undo, balance check) | Transactions `Import statement`; Bank Accounts `Import statement` |
| 9 | Matching: many documents per line, six difference reasons, must balance to the rupee, unmatch with reason | Transactions / Reconciliation `Match` |
| 10 | TDS payable (at payment), receivable (at bank match), challans, certificates, rates with history | `/admin/billing/tds` |
| 11 | GST: this month, sales and purchase registers, GSTR-2B reconciliation, return tracker, rate history | `/admin/billing/gst` |
| 12 | Expenses: department, receipts, approval by limits, recurring drafts, reimbursement; Petty Cash | Expenses (EX3); `/admin/billing/petty-cash` |
| 13 | Compliance calendar with live amounts, reminders (logged, OFF by default), period lock | `/admin/billing/compliance`, `/admin/billing/period-locks` |
| 15–19 | Data model, API, permissions, messages, settings, master data | below |
| 20 | Accounting export (Excel, one sheet per list) | Compliance Calendar `Accounting export`; `GET /api/export/accounting` |

## New backend files (copy as they are)

```
Regalis.Domain/Entities/Finance/Billing/{Receivables,Payables,Bank,Tax,ExpenseControls,Compliance}.cs
Regalis.Application/Finance/Billing/{Receivable,Payable,Ledger,Bank,Tax,ExpenseControl,Compliance}Dtos.cs
Regalis.Infrastructure/Persistence/Configurations/BillingFinanceConfigurations.cs
Regalis.Infrastructure/Persistence/Interceptors/BillingFinanceRulesInterceptor.cs
Regalis.Infrastructure/Persistence/Migrations/20261101165945_BillingFinance{,.Designer}.cs
Regalis.Infrastructure/Services/Billing/*.cs          (13 services, FinanceRules, BillingFinanceRegistration)
Regalis.API/Controllers/{CustomerPayments,Payables,Parties,BankReconciliation,Tax,ExpenseControls,Compliance}Controller.cs
```

Namespaces are `Regalis.Domain.Entities.Finance.Billing`, `Regalis.Application.Finance.Billing`,
`Regalis.Infrastructure.Services.Billing`. Several small classes share a file (one file per area).

## Edit to an existing file

`Infrastructure/DependencyInjection.cs` (exact change in `backend/existing-files.diff`):

1. `using Regalis.Infrastructure.Services.Billing;`
2. append `provider.GetRequiredService<BillingFinanceRulesInterceptor>()` to the
   `dbOptions.AddInterceptors(...)` call (after the existing three);
3. `services.AddBillingFinance();` before `return services;`.

Nothing else in the existing source changes. The new data sits in new tables, including 1:1
side tables where the doc adds columns to existing records (supplier PAN/state/TDS/terms →
`MST_SupplierTaxProfile`; customer TAN / do-not-chase → `MST_CustomerTaxProfile`; expense
department/approval/who-paid → `TRN_ExpenseControl`), so the existing entities, configurations
and services are untouched and the source drift between `Regalis` and `Regalis-live` does not
matter.

**Why an interceptor and not triggers.** The period lock and "reconciled means locked" must hold
on the existing endpoints too (existing Expenses, Invoices, Bank). EF Core cannot save to a table
that has database triggers unless every existing configuration declares them, so the rules live
in `BillingFinanceRulesInterceptor` (a `SaveChangesInterceptor`), which sees every write the app
makes: a locked month refuses invoices, expenses, expense adjustments and bank lines dated in it;
an expense matched to a reconciled line is frozen; a line reconciled on the new screen can only be
unmatched there; an unapproved expense cannot be matched (old screen too); a supplier with bills
cannot be deleted; every new expense gets its approval row; an approved expense whose amount grows
past its approved level goes back for approval.

## Migration

`20261101165945_BillingFinance` was generated with `dotnet ef migrations add` against the current
model snapshot (decompiled). It creates 29 tables and nothing else. In the source, the cleanest
route is to copy the files and run

```
dotnet ef migrations add BillingFinance --project src\Regalis.Infrastructure --startup-project src\Regalis.API
```

after deleting the copied migration, so the snapshot is regenerated from the real source; the
output should again be 29 `CreateTable`s. The SQL in `db/migrations` is the idempotent script of
the same migration (`--idempotent`), which is what the deploy runs.

`20261101170000_BillingFinanceAccess.sql` (one transaction, idempotent) adds:

- **Permissions** `PAYMENT.RECORD`, `PAYMENT.REVERSE`, `BILL.VIEW/CREATE/APPROVE/PAY`,
  `BANK.IMPORT/MATCH/WRITEOFF`, `TDS.VIEW/MANAGE`, `GST.VIEW/MANAGE`, `PETTYCASH.MANAGE`,
  `EXPENSE.APPROVE/REIMBURSE`, `COMPLIANCE.VIEW/MANAGE`, `FINANCE.LOCK/UNLOCK/EXPORT`.
- **Grants**: ADMIN all; FINANCE all but `FINANCE.UNLOCK` and `BANK.WRITEOFF`; MANAGER
  `PAYMENT.RECORD` (cash and UPI only at the desk - enforced in the API).
- **Roles** ACCOUNTANT (every finance view + exports + files returns: `GST.MANAGE`, `TDS.MANAGE`,
  `COMPLIANCE.MANAGE`) and AUDITOR (every finance view + exports + `AUDIT.VIEW/EXPORT`, nothing else).
- **Sidebar**: five bands, six pages, Reconciliation restored, existing pages ordered into bands
  (no page renamed or moved out of Billing & Finance).
- **Settings** (all editable in Settings): `Expense.ApprovalLimitNone` 2000,
  `Expense.ApprovalLimitManager` 25000, `Expense.ReceiptRequiredAbove` 2000, `Bill.ApprovalLimit`
  25000, `Bill.DefaultPaymentTermsDays` 15, `Bank.RoundingCap` 10, `Bank.ChargeCategory`,
  `PettyCash.Float` 5000, `Reminders.*` (master switch **false**), `Compliance.ProfessionalTaxAmount`.
- **Master data**: departments, TDS sections with effective dates (194C 2%/1%, 194J 10%,
  194I(b) 10%, 194I(a) 2%, 194H 5%, 194Q 0.1%, no-PAN 20%), GST rates, the six difference
  reasons, the five compliance obligations. Existing expenses are marked approved.
- **Sequences** `InvoiceReceipt` (REG/RCP), `PurchaseBill` (REG/PB), `VendorPayment` (REG/VP).

## Decisions and deviations, stated plainly

- **Paid / Outstanding** come from `TRN_Invoice.PaidAmount`, which only payments move (the existing
  code already kept it; a second stored figure was not added).
- **Reversals** write a negative receipt row pointing at the original; the PAY- payment becomes
  Refunded with the reason. Wallet payments go back to the wallet. Nothing is hard-deleted.
- **Cash** recorded against a booking or pass invoice goes into the collector's Cash Register
  session; it can only be voided by reversing the payment. Cash against a quotation invoice is
  receipted but not put in a drawer, because the existing Cash Collections screens only know
  bookings and passes.
- **TDS.VIEW / GST.VIEW are read-only**; writes need `TDS.MANAGE` / `GST.MANAGE`. The doc lets
  TDS.VIEW record challans, but its Auditor role must change nothing; both cannot hold with one code.
- **Approval levels**: up to `ApprovalLimitNone` automatic; up to `ApprovalLimitManager` anyone
  with `EXPENSE.APPROVE`; above it an Administrator ("owner"). Never the person who raised it.
- **Matching signs**: for money in, TDS, bank charge and short payment reduce what the bank got;
  interest and advance add to it; rounding carries its own sign. Short payment and interest are not
  offered on money out. A bank charge becomes an approved expense in `Bank.ChargeCategory`; an
  advance becomes a wallet credit (customer) or a vendor advance (supplier).
- **Bank-import undo** soft-deletes the lines and moves each bank reference into the remarks, so the
  same lines can be imported again (the reference is unique per account).
- **Reminders** are email only and logged per document; WhatsApp is not wired (doc open question 6).
  They stay off until `Reminders.Enabled` is true, which should wait until email is proven to send.
- **Not built**: Tally XML and Zoho CSV (doc §20.2 says Excel first), debit-note screen (§21),
  adding Outstanding/Ageing to the report library (they have their own page and Excel), a
  separate supplier detail page (supplier bills are on the supplier's ledger page).

## What was tested

On `RegalisWorkSpace_BFTest`, a copy of Dev with both migrations applied, using a test API built from
the decompiled source plus this kit:

- `tests/e2e_billing.py`: 71 checks, all passing. They cover the Record payment validations, wallet
  and UPI payments, reversal, and the register. They cover bills: the PAN format, the GST split, TDS,
  the duplicate 409, HSN, and the approval limit with segregation of duty. They cover vendor payments
  with TDS, challans, the supplier ledger and ageing, and bank import (preamble rows, duplicates,
  re-import adds nothing). They cover matching with a bank charge and with customer TDS, refusing an
  unbalanced match, the rounding cap, undo blocked once matched, the old unmatch blocked, and unmatch
  reversing receipts and TDS. They also cover expense approval and petty cash, the period lock (the
  interceptor refusing a back-dated expense; unlock with a reason), GSTR-2B upload, the compliance
  calendar, and the accounting export and statement PDF.
- `tests/ui_tour.py` / `ui_dialogs.py`: headless Chrome against the patched SPA. All 23 finance pages
  render with no JavaScript errors, the sidebar shows the five bands, and the Record payment, Match,
  New vendor payment and Import dialogs open with live data.

## Rollback

`db\migrations\20261101170000_BillingFinanceAccess_Rollback.sql`, then
`20261101165945_BillingFinance_Rollback.sql` (drops the 29 tables and their data), then restore the
API and frontend backups the deploy script made.
