# Pay later at the front desk — deploy kit

Design and decisions: "Pay Later (Cash) — Feature Design & Implementation Plan"
(https://claude.ai/artifact/5eRTZzLMtLYxB9iy222NqD), decisions of 27 Oct 2026.

This repo is the deploy snapshot; the API is built from
`C:\Users\Administrator\Downloads\Regalis-live\backend` on the UAT server. The backend
part of this kit therefore has to be applied to that source first. Everything here was
compiled against the current API (decompiled from `api\*.dll`), run against a copy of
the database, and tested end to end (see **What was tested**).

| Part | Where | How it goes in |
|---|---|---|
| New backend files | `backend/src/...` (same layout as the source tree) | copy into `Regalis-live\backend\src\` |
| Edits to 21 existing backend files | this README, **Edits to existing files**; exact reference in `backend/existing-files.diff` | apply by hand |
| EF migration | `backend/src/Regalis.Infrastructure/Persistence/Migrations/20261027182059_PayLaterCash*.cs` | see **Migration** |
| Database scripts | `db/migrations/20261027182059_PayLaterCash.sql` (+ `_Rollback`), `20261027182100_PayLaterAccess.sql` (+ `_Rollback`) | run by `deploy-pay-later.cmd` |
| Front end | `frontend/apply.mjs` + `frontend/assets/AdminCashPages-PL1.js` | run by `deploy-pay-later.cmd` |
| Deploy | `deploy-pay-later.cmd` (repo root), `verify.ps1` | run elevated on UAT |
| End-to-end test | `tests/e2e_paylater.py` | optional, against a test API |

## Ships switched off

`PayLater.Enabled = false` and `Wallet.DeskAddMoneyEnabled = true`, so the deploy changes
nothing for customers or the desk. Go-live is two settings (Admin > Settings):
`PayLater.Enabled = true` and `Wallet.DeskAddMoneyEnabled = false`. Settings are cached
for 60 seconds.

Other settings (group `PayLater`): `AllowedBookingKinds` (`Hourly,MultiDay,Monthly`),
`MaxAmount` and `MaxOutstandingPerCustomer` (`0` = no limit), `MaxOpenBookings` (`2`;
a multi-day checkout counts once), `DueBeforeStartMinutes` (`0`),
`AutoCancelAfterStartMinutes` (`30`), `BlockCheckInUntilPaid` (`true`),
`ReminderHoursBefore` (`24`), `AutoBlockAfterUnpaidNoShows` (`3`).

## New backend files (copy as they are)

```
Regalis.Domain/Entities/Finance/CashCollection.cs
Regalis.Domain/Entities/Finance/CashRegisterSession.cs
Regalis.Domain/Enums/CashCollectionStatus.cs
Regalis.Domain/Enums/CashSessionStatus.cs
Regalis.Application/Finance/CashDtos.cs
Regalis.Application/Finance/CashValidators.cs
Regalis.Application/Finance/ICashCollectionService.cs
Regalis.Infrastructure/Services/PayLaterPolicy.cs
Regalis.Infrastructure/Services/CashCollectionService.cs
Regalis.Infrastructure/Services/CashRegisterService.cs
Regalis.Infrastructure/BackgroundJobs/PayLaterSweeper.cs
Regalis.Infrastructure/Reporting/CashReceiptPdfGenerator.cs
Regalis.Infrastructure/Persistence/Configurations/CashCollectionConfiguration.cs
Regalis.Infrastructure/Persistence/Configurations/CashRegisterSessionConfiguration.cs
Regalis.API/Controllers/CashController.cs
```

Check the folder names against the source tree (the decompiled namespaces were
`Regalis.Domain.Entities.Finance`, `Regalis.Infrastructure.Services`, and so on).

## Edits to existing files

`backend/existing-files.diff` is the exact change, made on decompiled code (so the
surrounding lines look different from the source). The same edits in plain terms:

### Domain

1. `Enums/BookingPaymentStatus.cs` — add `PartiallyPaid` (= 6) after `PartiallyRefunded`.
2. `Entities/Booking/Booking.cs` — add, before `RowVersion`:
   ```csharp
   public bool IsPayLater { get; set; }
   public DateTime? PaymentDueUtc { get; set; }
   public decimal AmountPaid { get; set; }
   public DateTime? PayLaterReminderSentUtc { get; set; }
   ```
3. `Entities/Identity/User.cs` — add `public bool PayLaterAllowed { get; set; } = true;`

### Application / Shared

4. `Bookings/PaymentMethods.cs` — `public const string PayLater = "paylater";`
5. `Bookings/ConfirmBookingRequestValidator.cs` — allow `"paylater"`.
6. `Bookings/AdminCreateBookingRequestValidator.cs` — allow `"paylater"`.
7. `Bookings/BookingDto.cs` — append optional parameters
   `bool IsPayLater = false, decimal AmountPaid = 0m, DateTime? PaymentDueUtc = null`.
8. `Bookings/ConfirmBookingResponse.cs` — append `bool IsPayLater = false`.
9. `Bookings/IBookingService.cs` — add
   `Task<PayLaterStatusDto> GetPayLaterStatusAsync(CancellationToken ct = default);` and
   `Task<PayLaterEligibilityDto> GetPayLaterEligibilityAsync(long bookingId, CancellationToken ct = default);`
10. `Abstractions/SequenceKeys.cs` — `CashReceipt = "CashReceipt"`, `CashSession = "CashSession"`.
11. `Access/DenyReasons.cs` — `PaymentDue = "PaymentDue"`.
12. `Shared/Constants/PermissionCodes.cs` (class `Payment`) — `CashCollect`, `CashClose`,
    `CashVoid`, `CashApprove` = `"PAYMENT.CASH_COLLECT"` … `"PAYMENT.CASH_APPROVE"`.

### Infrastructure

13. `Persistence/RegalisDbContext.cs` — `DbSet<CashCollection> CashCollections` and
    `DbSet<CashRegisterSession> CashRegisterSessions`.
14. `Persistence/Configurations/BookingConfiguration.cs` — check constraint
    `CK_Booking_AmountPaid` (`[AmountPaid] >= 0`) and index `IX_Booking_PayLater_Due` on
    `PaymentDueUtc` including `CustomerId, Status, TotalAmount, AmountPaid`, filtered
    `[IsPayLater] = 1 AND [IsDeleted] = 0`.
15. `Persistence/Configurations/UserConfiguration.cs` —
    `builder.Property(x => x.PayLaterAllowed).HasDefaultValue(true);`
16. `DependencyInjection.cs` — register `ICashCollectionService`/`CashCollectionService`,
    `ICashRegisterService`/`CashRegisterService` (scoped) and
    `AddHostedService<PayLaterSweeper>()`.
17. `Services/BookingService.cs`
    - `ConfirmAsync` and `AdminCreateAsync`: just before `SettleAndFinalizeAsync`, when the
      method is `paylater`, call `PayLaterPolicy.CheckAsync(db, settings, booking, ct)` and
      throw `BusinessRuleException(reason, "PAY_LATER_NOT_ALLOWED")` when it returns a reason.
    - `SettleAndFinalizeAsync`: `"paylater" => null` in the payment switch (payment becomes
      `Payment?`); set `payment.InvoiceId` only when there is a payment; for pay later append
      "Please pay ₹… at the front desk (cash, UPI or card) before you check in." to the
      confirmation notification, say "Pay later: ₹… to collect at the front desk." in the
      admin alert, set `IsPayLater = true`, `AmountPaid = 0`, `PaymentStatus = Pending`,
      `PaymentDueUtc = await PayLaterPolicy.DueUtcAsync(...)`, and use the history reason
      "Confirmed with pay later; ₹… due at the front desk."; return
      `payment?.PaymentId ?? 0`, `payment?.PaymentNumber ?? ""` and `IsPayLater`.
    - `BuildInvoiceAsync(Booking, Payment?)`: with no payment the invoice is `Pending`,
      `PaidAmount = 0`, `PaymentId = null`, `PaidUtc = null`.
    - `ToDto`: pass `b.IsPayLater, b.AmountPaid, b.PaymentDueUtc`.
    - New `GetPayLaterStatusAsync` (customers only; `PayLaterPolicy.CustomerStatusAsync`)
      and `GetPayLaterEligibilityAsync` (the caller's own Pending hold;
      `PayLaterPolicy.CheckAsync`).
18. `Services/AccessService.cs` — inject `ISettingService settings`. In the token rules,
    after the wrong-workspace check, refuse entry (`DenyReasons.PaymentDue`) when the
    booking is pay later with a balance and `PayLater.BlockCheckInUntilPaid` is on. In
    `ManualOverrideAsync`, refuse an IN override for such a booking with
    `BusinessRuleException(..., "PAYMENT_DUE")`.
19. `Services/BookingMutationService.cs` (`CancelAsync`) — refund base is
    `IsPayLater ? AmountPaid : TotalAmount`; refund also when `PartiallyPaid`; when a pay-later
    booking with nothing paid is cancelled, cancel its `Pending` invoice.
20. `Services/WalletDeskService.cs` (`AddMoneyAsync`) — refuse with
    `DESK_TOPUP_DISABLED` when `Wallet.DeskAddMoneyEnabled` is false.

### API

21. `Controllers/BookingsController.cs` — `GET api/bookings/pay-later` (status, before a
    hold) and `GET api/bookings/{id:long}/pay-later` (eligibility of a hold), both
    `BOOKING.CREATE`.

## Migration

The model change is `20261027182059_PayLaterCash` (2 tables, 5 columns, 1 check constraint,
indexes). It was generated with `dotnet ef migrations add` against the current model
snapshot, and the snapshot it produced matched the live model exactly (the migration
contains nothing but Pay later).

In the source, the cleanest route is to apply the edits above and run

```
dotnet ef migrations add PayLaterCash --project src\Regalis.Infrastructure --startup-project src\Regalis.API
```

which also updates `RegalisDbContextModelSnapshot.cs`; its `Up()` should match the two
files in this kit. Keep the id `20261027182059_PayLaterCash` (rename the generated files
and the `[Migration]` attribute) so it matches the `.sql` in `db\migrations`, which is
what the deploy runs (UAT has `MigrateOnStartup` off).

## What was tested

On a copy of the dev database (`RegalisWorkSpace_PayLaterTest`), with the API built from
the decompiled source plus this kit and the patched SPA:

- `tests/e2e_paylater.py`: 35 checks — confirm with pay later (unpaid invoice, QR issued),
  gate and manual override refuse check-in while money is due, the open-bookings limit
  (status, eligibility and confirm), part cash payment with change, over-payment and
  UPI-without-reference refused, UPI balance → paid booking and invoice, gate lets the paid
  booking in, void reverses every total and cancels the payment, second void refused,
  receipt PDF, outstanding and receipt lists, customers kept out of `/api/cash`, register
  auto-opens, close needs a reason for a difference, Admin approval, a closed register
  refuses more receipts, cancelling an unpaid pay-later booking cancels its invoice, desk
  top-up switched off, admin booking with pay later, customer cash still refused.
- Background job: reminder sent once; unpaid booking released 30 min after start (booking
  and invoice cancelled, QR revoked, customer told); customer blocked at the threshold.
- Both migrations applied twice (idempotent) and rolled back.
- SPA (headless Edge, no console errors): Cash Collections (both tabs, Take payment dialog),
  Cash Register, sidebar entries and icons, My Bookings "Pay later · ₹… due", checkout
  button shown/hidden by the limit, confirmation screen.

Not tested: the real source build (not on this machine), IIS, and the customer's
multi-day and monthly pay-later checkouts in the browser (the API paths are the same).
