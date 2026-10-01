using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Reporting;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The admin's view of one customer's wallet, and the two desk actions on it.
///
/// Totals are summed from the append-only ledger; the stored MST_Wallet balance is only
/// compared against it (LedgerVerified). Nothing here edits or deletes a ledger row:
/// Add money is a PAY- payment plus a Recharge credit, written in ONE database
/// transaction so a credit can never exist without the payment behind it; Adjust is a
/// new AdminAdjustment entry, with the reason and the admin's name on the audit trail.
/// Debits go through IWalletService.DebitAsync, which refuses to take a balance below
/// zero (and MST_Wallet's CHECK constraint backs that up).
/// </summary>
public class WalletDeskService(
    RegalisDbContext db,
    IWalletService walletService,
    INumberSequenceService sequences,
    ISettingService settings,
    IPaymentService paymentService,
    IQuotationDispatcher dispatcher,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<WalletDeskService> logger) : IWalletDeskService
{
    private static readonly Dictionary<string, WalletCategory[]> CategoryGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        ["recharge"] = [WalletCategory.Recharge],
        ["booking"] = [WalletCategory.Booking],
        ["refund"] = [WalletCategory.Refund, WalletCategory.Cancellation],
        ["day-pass"] = [WalletCategory.DayPass],
        ["monthly-pass"] = [WalletCategory.MonthlyPass],
        ["service"] = [WalletCategory.ServiceOrder],
        ["deposit"] = [WalletCategory.SecurityDeposit],
        ["adjustment"] = [WalletCategory.AdminAdjustment]
    };

    public static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Upi => "UPI",
        PaymentMethod.CreditCard => "Credit card",
        PaymentMethod.DebitCard => "Debit card",
        PaymentMethod.NetBanking => "Bank transfer",
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Wallet => "Wallet",
        _ => "Other"
    };

    // ── Read ─────────────────────────────────────────────────────────────────

    public async Task<WalletDetailDto> GetWalletAsync(long customerId, WalletLedgerRequest request,
        CancellationToken ct = default)
    {
        var customer = await LoadCustomerAsync(customerId, ct);
        var (credit, debit) = await LedgerTotalsAsync(customerId, ct);
        var balance = credit - debit;
        var stored = await db.Wallets.AsNoTracking().Where(w => w.CustomerId == customerId)
            .Select(w => (decimal?)w.Balance).FirstOrDefaultAsync(ct) ?? 0m;
        var threshold = await settings.GetDecimalAsync(AppSettingKeys.Wallet.LowBalanceThreshold, 100m, ct);

        var query = Filter(db.WalletTransactions.AsNoTracking().Where(t => t.CustomerId == customerId), request);
        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(t => t.TransactionUtc).ThenByDescending(t => t.WalletTransactionId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new
            {
                Txn = t,
                Payment = db.Payments.Where(p => p.PaymentId == t.PaymentId)
                    .Select(p => new { p.PaymentNumber, p.Status }).FirstOrDefault(),
                By = db.Users.Where(u => u.UserId == t.PerformedByUserId).Select(u => new { u.FullName, u.UserType }).FirstOrDefault()
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new WalletLedgerEntryDto(
            r.Txn.WalletTransactionId, r.Txn.TransactionNumber, (byte)r.Txn.Direction, (byte)r.Txn.Category,
            r.Txn.Amount, r.Txn.BalanceAfter, r.Txn.Narration, r.Txn.TransactionUtc,
            // Receipt only where a real payment stands behind a recharge.
            r.Txn.Category == WalletCategory.Recharge && r.Payment?.Status == PaymentStatus.Success ? r.Txn.PaymentId : null,
            r.Payment?.PaymentNumber,
            // Name the admin behind a manual entry; the customer's own actions need no label.
            r.By is not null && r.By.UserType != UserType.Customer ? r.By.FullName : null)).ToList();

        return new WalletDetailDto(customer, credit, debit, balance, balance < threshold, threshold,
            LedgerVerified: stored == balance,
            PagedResult<WalletLedgerEntryDto>.Create(items, request.Page, request.PageSize, total),
            await sequences.PeekAsync(SequenceKeys.Payment, ct));
    }

    // ── Add money ────────────────────────────────────────────────────────────

    public async Task<AdminAddMoneyResponse> AddMoneyAsync(long customerId, AdminAddMoneyRequest request,
        CancellationToken ct = default)
    {
        var customer = await LoadCustomerAsync(customerId, ct);
        var method = (PaymentMethod)request.PaymentMethod;
        var today = IstClock.IstDateOf(clock.UtcNow);
        var receivedOn = request.ReceivedOn ?? today;
        if (receivedOn > today || receivedOn < today.AddDays(-365))
        {
            throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "The received date must be within the last year and not in the future.", "receivedOn")]);
        }
        var paidUtc = receivedOn == today
            ? clock.UtcNow
            : IstClock.ToUtc(receivedOn.ToDateTime(new TimeOnly(12, 0)));
        var reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();

        var strategy = db.Database.CreateExecutionStrategy();
        var (payment, transactionNumber) = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var payment = new Payment
            {
                PaymentNumber = await sequences.NextAsync(SequenceKeys.Payment, ct),
                CustomerId = customerId,
                Amount = request.Amount,
                PaymentMethod = method,
                // Collected at the desk: no gateway. The reference/UTR rides in the
                // gateway-payment-id column, the only free-text reference Payment has.
                Gateway = "Desk",
                GatewayPaymentId = reference,
                IsSignatureVerified = false,
                Status = PaymentStatus.Success,
                PaidUtc = paidUtc,
                RawGatewayResponse = JsonSerializer.Serialize(new
                {
                    source = "admin-desk",
                    recordedByUserId = currentUser.UserId,
                    recordedBy = currentUser.FullName ?? currentUser.Email,
                    receivedOn = receivedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    reference
                })
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);

            var narration = NarrationFor(request, payment.PaymentNumber, method);
            await walletService.CreditAsync(customerId, request.Amount, WalletCategory.Recharge, narration,
                referenceType: "Payment", referenceId: payment.PaymentId, paymentId: payment.PaymentId, ct: ct);
            await db.SaveChangesAsync(ct);

            var number = await db.WalletTransactions.Where(t => t.PaymentId == payment.PaymentId)
                .Select(t => t.TransactionNumber).FirstAsync(ct);
            AddAudit("WALLET_ADD_MONEY", customerId, new
            {
                payment.PaymentNumber,
                transactionNumber = number,
                request.Amount,
                method = MethodLabel(method),
                reference,
                receivedOn = receivedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (payment, number);
        });

        var (_, _, balanceNow) = await BalanceAsync(customerId, ct);
        logger.LogInformation("Desk recharge {PaymentNumber} of {Amount} for customer {CustomerId} by {AdminId}",
            payment.PaymentNumber, request.Amount, customerId, currentUser.UserId);

        string? note = null;
        if (request.NotifyCustomer)
        {
            note = await NotifyAsync(customer, payment, balanceNow, ct);
        }
        return new AdminAddMoneyResponse(payment.PaymentNumber, payment.PaymentId, transactionNumber, balanceNow, note);
    }

    // ── Adjust ───────────────────────────────────────────────────────────────

    public async Task<WalletDetailDto> AdjustAsync(long customerId, AdminAdjustRequest request,
        CancellationToken ct = default)
    {
        await LoadCustomerAsync(customerId, ct);
        var narration = request.Narration.Trim();
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (request.Direction == "credit")
            {
                await walletService.CreditAsync(customerId, request.Amount, WalletCategory.AdminAdjustment,
                    narration, ct: ct);
            }
            else
            {
                // Throws INSUFFICIENT_WALLET_BALANCE rather than go below zero.
                await walletService.DebitAsync(customerId, request.Amount, WalletCategory.AdminAdjustment,
                    narration, ct: ct);
            }
            await db.SaveChangesAsync(ct);
            AddAudit("WALLET_ADJUST", customerId, new
            {
                request.Direction,
                request.Amount,
                reason = request.Reason.Trim(),
                narration
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        });

        logger.LogWarning("Wallet {Direction} adjustment of {Amount} for customer {CustomerId} by {AdminId}: {Reason}",
            request.Direction, request.Amount, customerId, currentUser.UserId, request.Reason);
        return await GetWalletAsync(customerId, new WalletLedgerRequest(), ct);
    }

    // ── Statement ────────────────────────────────────────────────────────────

    public async Task<(byte[] Content, string ContentType, string FileName)> ExportStatementAsync(
        long customerId, WalletLedgerRequest request, string format, CancellationToken ct = default)
    {
        var customer = await LoadCustomerAsync(customerId, ct);
        var all = db.WalletTransactions.AsNoTracking().Where(t => t.CustomerId == customerId);
        var fromUtc = request.From is null ? (DateTime?)null : IstClock.ToUtc(request.From.Value.ToDateTime(TimeOnly.MinValue));
        var toUtc = request.To is null ? (DateTime?)null : IstClock.ToUtc(request.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));

        // A statement is a period: opening balance, every entry in date order, closing balance.
        var opening = fromUtc is null
            ? 0m
            : await all.Where(t => t.TransactionUtc < fromUtc)
                .OrderByDescending(t => t.TransactionUtc).ThenByDescending(t => t.WalletTransactionId)
                .Select(t => (decimal?)t.BalanceAfter).FirstOrDefaultAsync(ct) ?? 0m;
        var inPeriod = all;
        if (fromUtc is not null) inPeriod = inPeriod.Where(t => t.TransactionUtc >= fromUtc);
        if (toUtc is not null) inPeriod = inPeriod.Where(t => t.TransactionUtc < toUtc);
        var entries = await inPeriod
            .OrderBy(t => t.TransactionUtc).ThenBy(t => t.WalletTransactionId)
            .Take(20_000)
            .Select(t => new WalletStatementLine(t.TransactionNumber, t.TransactionUtc, t.Narration,
                t.Category, t.Direction, t.Amount, t.BalanceAfter))
            .ToListAsync(ct);
        var closing = entries.Count > 0 ? entries[^1].BalanceAfter : opening;
        var stamp = IstClock.IstDateOf(clock.UtcNow).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var safeName = new string(customer.FullName.Where(char.IsLetterOrDigit).ToArray());

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = new StringBuilder();
            csv.AppendLine($"Wallet statement,{Cell(customer.FullName)},{Cell(customer.Email)}");
            csv.AppendLine($"Period,{request.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "start"},{request.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "today"}");
            csv.AppendLine($"Opening balance,{Money(opening)}");
            csv.AppendLine("Txn,When (IST),Narration,Category,Credit,Debit,Balance");
            foreach (var e in entries)
            {
                csv.AppendLine(string.Join(",", e.Number,
                    IstClock.ToIst(e.Utc).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    Cell(e.Narration), WalletStatementPdfGenerator.CategoryLabel(e.Category),
                    e.Direction == WalletDirection.Credit ? Money(e.Amount) : "",
                    e.Direction == WalletDirection.Debit ? Money(e.Amount) : "", Money(e.BalanceAfter)));
            }
            csv.AppendLine($"Closing balance,{Money(closing)}");
            return (Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
                "text/csv", $"wallet-statement-{safeName}-{stamp}.csv");
        }

        var pdf = WalletStatementPdfGenerator.Generate(new WalletStatementPdfModel(
            CompanyName: await settings.GetStringAsync("Company.Name", "Regalis WorkSpace", ct),
            CompanyAddress: await settings.GetStringAsync("Company.Address", "", ct),
            CompanyGstin: await settings.GetStringAsync("Company.Gstin", "", ct),
            CustomerName: customer.FullName,
            CustomerEmail: customer.Email,
            CustomerMobile: customer.MobileNumber,
            From: request.From,
            To: request.To ?? IstClock.IstDateOf(clock.UtcNow),
            Opening: opening,
            Closing: closing,
            Lines: entries,
            GeneratedUtc: clock.UtcNow,
            Logo: await PdfLogo.LoadAsync(settings, ct)));
        return (pdf, "application/pdf", $"wallet-statement-{safeName}-{stamp}.pdf");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<WalletCustomerDto> LoadCustomerAsync(long customerId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.UserId == customerId && u.UserType == UserType.Customer)
            .Select(u => new { u.UserId, u.FullName, u.Email, u.MobileNumber, u.CreatedDate })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(User), customerId);
        var walletOpened = await db.Wallets.AsNoTracking().Where(w => w.CustomerId == customerId)
            .Select(w => (DateTime?)w.CreatedDate).FirstOrDefaultAsync(ct);
        var today = IstClock.IstDateOf(clock.UtcNow);
        var pass = await db.CustomerPasses.AsNoTracking()
            .Where(p => p.CustomerId == customerId && p.Status == CustomerPassStatus.Active
                        && p.ValidFrom <= today && p.ValidTo >= today)
            .OrderByDescending(p => p.ValidTo)
            .Select(p => new { p.PassPlan.PlanName, p.ValidTo })
            .FirstOrDefaultAsync(ct);
        return new WalletCustomerDto(user.UserId, user.FullName, user.Email, user.MobileNumber,
            user.CreatedDate, walletOpened, pass?.PlanName, pass?.ValidTo);
    }

    private async Task<(decimal Credit, decimal Debit)> LedgerTotalsAsync(long customerId, CancellationToken ct)
    {
        var sums = await db.WalletTransactions.AsNoTracking()
            .Where(t => t.CustomerId == customerId)
            .GroupBy(t => t.CustomerId)
            .Select(g => new
            {
                Credit = g.Sum(t => t.Direction == WalletDirection.Credit ? t.Amount : 0m),
                Debit = g.Sum(t => t.Direction == WalletDirection.Debit ? t.Amount : 0m)
            })
            .FirstOrDefaultAsync(ct);
        return (sums?.Credit ?? 0m, sums?.Debit ?? 0m);
    }

    private async Task<(decimal Credit, decimal Debit, decimal Balance)> BalanceAsync(long customerId, CancellationToken ct)
    {
        var (credit, debit) = await LedgerTotalsAsync(customerId, ct);
        return (credit, debit, credit - debit);
    }

    private IQueryable<WalletTransaction> Filter(IQueryable<WalletTransaction> query, WalletLedgerRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(t => t.TransactionNumber.Contains(term) || t.Narration.Contains(term));
        }
        if (string.Equals(request.Direction, "credit", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => t.Direction == WalletDirection.Credit);
        }
        else if (string.Equals(request.Direction, "debit", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => t.Direction == WalletDirection.Debit);
        }
        if (!string.IsNullOrWhiteSpace(request.Category) && CategoryGroups.TryGetValue(request.Category.Trim(), out var group))
        {
            query = query.Where(t => group.Contains(t.Category));
        }
        if (request.From is not null)
        {
            var fromUtc = IstClock.ToUtc(request.From.Value.ToDateTime(TimeOnly.MinValue));
            query = query.Where(t => t.TransactionUtc >= fromUtc);
        }
        if (request.To is not null)
        {
            var toUtc = IstClock.ToUtc(request.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(t => t.TransactionUtc < toUtc);
        }
        return query;
    }

    private static string NarrationFor(AdminAddMoneyRequest request, string paymentNumber, PaymentMethod method)
    {
        var fallback = $"Wallet recharge {paymentNumber} · {MethodLabel(method)}";
        if (string.IsNullOrWhiteSpace(request.Narration))
        {
            return fallback;
        }
        var text = request.Narration.Trim();
        // The form previewed the next number; if another payment took it, say the real one.
        if (!string.IsNullOrWhiteSpace(request.ExpectedPaymentNumber) && request.ExpectedPaymentNumber != paymentNumber)
        {
            text = text.Replace(request.ExpectedPaymentNumber, paymentNumber, StringComparison.Ordinal);
        }
        return text;
    }

    private void AddAudit(string action, long customerId, object details) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.IsAuthenticated ? currentUser.UserId : null,
            UserName = currentUser.FullName ?? currentUser.Email,
            Action = action,
            ModuleName = "Finance",
            EntityName = nameof(Wallet),
            EntityId = customerId.ToString(CultureInfo.InvariantCulture),
            NewValue = JsonSerializer.Serialize(details),
            IpAddress = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent,
            CorrelationId = currentUser.CorrelationId,
            TimestampUtc = clock.UtcNow
        });

    /// <summary>In-app notification, plus the receipt by email. A delivery failure is
    /// reported back to the admin, never thrown: the money is already recorded.</summary>
    private async Task<string> NotifyAsync(WalletCustomerDto customer, Payment payment, decimal balance,
        CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        db.Notifications.Add(new Notification
        {
            UserId = customer.CustomerId,
            Audience = NotificationAudience.SingleUser,
            Title = "Money added to your wallet",
            Message = $"₹{payment.Amount:N2} was added to your wallet ({payment.PaymentNumber}). Balance: ₹{balance:N2}.",
            Category = NotificationCategory.Payment,
            ReferenceType = "Payment",
            ReferenceId = payment.PaymentId,
            ActionLabel = "View wallet",
            ActionRoute = "/wallet",
            SentUtc = clock.UtcNow
        });
        await db.SaveChangesAsync(ct);

        try
        {
            var receipt = await paymentService.GetReceiptPdfAsync(payment.PaymentId, ct);
            var result = await dispatcher.SendEmailAsync(new QuotationEmailMessage(
                customer.Email, null, null,
                $"Receipt {payment.PaymentNumber} — ₹{payment.Amount:N2} added to your Regalis wallet",
                $"Dear {customer.FullName},\n\nWe have received ₹{payment.Amount:N2} ({MethodLabel(payment.PaymentMethod)}) " +
                $"and added it to your Regalis WorkSpace wallet. Your balance is now ₹{balance:N2}.\n\n" +
                $"The receipt {payment.PaymentNumber} is attached.\n\nRegalis WorkSpace",
                [new QuotationOutboundFile($"{payment.PaymentNumber}.pdf", "application/pdf", receipt)]), ct);
            return result.Succeeded
                ? $"The customer was notified and the receipt emailed to {customer.Email}."
                : $"The customer was notified in the app, but the receipt email failed: {result.FailureReason}";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Receipt email for {PaymentNumber} failed", payment.PaymentNumber);
            return "The customer was notified in the app, but the receipt email could not be sent.";
        }
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Cell(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && "=+-@".Contains(text[0])) text = "'" + text;
        return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }
}
