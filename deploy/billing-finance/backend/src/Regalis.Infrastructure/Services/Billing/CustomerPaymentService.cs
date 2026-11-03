using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Application.Finance.Billing;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Persistence.Interceptors;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>
/// Recording a customer payment (build doc §3): cash, UPI, bank transfer, cheque, card or
/// the customer's wallet, against one invoice, with no gateway in it. One serializable
/// transaction writes the PAY- payment, the numbered receipt, the wallet debit or the
/// cash-drawer entry, and moves the invoice (and a pay-later booking or pass behind it).
/// The outstanding amount is re-read inside the transaction, so two admins cannot push an
/// invoice past its total. A payment is never edited: it is reversed.
/// </summary>
public class CustomerPaymentService(
    RegalisDbContext db,
    INumberSequenceService sequences,
    IWalletService wallets,
    IPaymentService payments,
    IQuotationDispatcher dispatcher,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICustomerPaymentService
{
    public const string ReceiptSequence = "InvoiceReceipt";

    public static string ModeName(InvoicePaymentMode mode) => mode switch
    {
        InvoicePaymentMode.Cash => "Cash",
        InvoicePaymentMode.Upi => "UPI",
        InvoicePaymentMode.BankTransfer => "Bank transfer",
        InvoicePaymentMode.Cheque => "Cheque",
        InvoicePaymentMode.Card => "Card",
        InvoicePaymentMode.Wallet => "Wallet",
        InvoicePaymentMode.TdsCredit => "TDS by customer",
        InvoicePaymentMode.WriteOff => "Short payment accepted",
        _ => "Other"
    };

    private static string MethodName(PaymentMethod method) => method switch
    {
        PaymentMethod.Wallet => "Wallet",
        PaymentMethod.Upi => "UPI",
        PaymentMethod.CreditCard => "Credit card",
        PaymentMethod.DebitCard => "Debit card",
        PaymentMethod.NetBanking => "Net banking",
        PaymentMethod.Cash => "Cash",
        _ => "Other"
    };

    public static PaymentMethod MethodFor(InvoicePaymentMode mode) => mode switch
    {
        InvoicePaymentMode.Cash => PaymentMethod.Cash,
        InvoicePaymentMode.Upi => PaymentMethod.Upi,
        InvoicePaymentMode.BankTransfer => PaymentMethod.NetBanking,
        InvoicePaymentMode.Card => PaymentMethod.DebitCard,
        InvoicePaymentMode.Wallet => PaymentMethod.Wallet,
        _ => PaymentMethod.Other
    };

    public async Task<InvoicePaymentContextDto> GetContextAsync(long invoiceId, CancellationToken ct = default)
    {
        Invoice invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct)
            ?? throw new NotFoundException("Invoice", invoiceId);
        decimal walletBalance = await db.Wallets.AsNoTracking().Where(w => w.CustomerId == invoice.CustomerId)
            .Select(w => (decimal?)w.Balance).FirstOrDefaultAsync(ct) ?? 0m;
        string today = FinanceRules.Period(FinanceRules.Today(clock));
        bool locked = await db.Set<PeriodLock>().AsNoTracking().AnyAsync(p => p.Period == today && p.IsLocked, ct);
        return new InvoicePaymentContextDto(invoice.InvoiceId, invoice.InvoiceNumber, invoice.CustomerName, invoice.InvoiceDate,
            invoice.TotalAmount, invoice.PaidAmount, Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount), (byte)invoice.Status,
            walletBalance, await sequences.PeekAsync(ReceiptSequence, ct), locked,
            await BankAccountOptionsAsync(db, ct), await GetForInvoiceAsync(invoiceId, ct));
    }

    public static async Task<IReadOnlyList<BankAccountOptionDto>> BankAccountOptionsAsync(RegalisDbContext db, CancellationToken ct)
    {
        return await db.BankAccounts.AsNoTracking().Where(a => a.IsActive)
            .OrderBy(a => a.BankName).ThenBy(a => a.AccountCode)
            .Select(a => new BankAccountOptionDto(a.BankAccountId, a.BankName + " · " + a.AccountCode))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<InvoicePaymentDto>> GetForInvoiceAsync(long invoiceId, CancellationToken ct = default)
    {
        return await Project(db.Set<InvoicePayment>().AsNoTracking().Where(p => p.InvoiceId == invoiceId)
            .OrderBy(p => p.RecordedUtc).ThenBy(p => p.InvoicePaymentId)).ToListAsync(ct);
    }

    private IQueryable<InvoicePaymentDto> Project(IQueryable<InvoicePayment> query)
    {
        return from p in query
               join i in db.Invoices on p.InvoiceId equals i.InvoiceId
               join pay in db.Payments on p.PaymentId equals (long?)pay.PaymentId into payJoin
               from pay in payJoin.DefaultIfEmpty()
               join a in db.BankAccounts on p.BankAccountId equals (int?)a.BankAccountId into accJoin
               from a in accJoin.DefaultIfEmpty()
               join u in db.Users on p.RecordedByUserId equals u.UserId into userJoin
               from u in userJoin.DefaultIfEmpty()
               select new InvoicePaymentDto(
                   p.InvoicePaymentId, p.ReceiptNumber, p.InvoiceId, i.InvoiceNumber, p.CustomerId, i.CustomerName,
                   p.PaymentId, pay != null ? pay.PaymentNumber : null, p.Amount, p.PaidOn, (byte)p.Mode,
                   p.Mode == InvoicePaymentMode.Cash ? "Cash" : p.Mode == InvoicePaymentMode.Upi ? "UPI"
                       : p.Mode == InvoicePaymentMode.BankTransfer ? "Bank transfer" : p.Mode == InvoicePaymentMode.Cheque ? "Cheque"
                       : p.Mode == InvoicePaymentMode.Card ? "Card" : p.Mode == InvoicePaymentMode.Wallet ? "Wallet"
                       : p.Mode == InvoicePaymentMode.TdsCredit ? "TDS by customer" : "Short payment accepted",
                   p.Reference, p.BankAccountId, a != null ? a.BankName + " · " + a.AccountCode : null, p.Note,
                   p.IsReversed, p.ReverseReason, p.ReversedUtc, p.ReversalOfId, u != null ? u.FullName : null, p.RecordedUtc,
                   p.BankTransactionId != null
                   || db.BankTransactions.Any(t => t.MatchedPaymentId == p.PaymentId && t.ReconciliationStatus == ReconciliationStatus.Reconciled)
                   || db.Set<BankMatchAllocation>().Any(m => m.DocType == BankMatchDocType.CustomerPayment && m.DocId == p.PaymentId));
    }

    public async Task<RecordInvoicePaymentResult> RecordAsync(long invoiceId, RecordInvoicePaymentRequest request, CancellationToken ct = default)
    {
        decimal amount = FinanceRules.Round(request.Amount);
        if (amount <= 0m)
        {
            throw new BusinessRuleException("Enter the amount received.", "AMOUNT_REQUIRED");
        }
        if (request.Mode < 1 || request.Mode > 6)
        {
            throw new BusinessRuleException("Choose how the payment was made.");
        }
        var mode = (InvoicePaymentMode)request.Mode;
        string? reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
        if (reference == null && mode is not (InvoicePaymentMode.Cash or InvoicePaymentMode.Wallet))
        {
            throw new BusinessRuleException("Enter the reference for this payment mode.", "REFERENCE_REQUIRED");
        }
        if (request.BankAccountId == null && mode is InvoicePaymentMode.BankTransfer or InvoicePaymentMode.Cheque or InvoicePaymentMode.Card)
        {
            throw new BusinessRuleException("Choose which account the money went into.", "BANK_ACCOUNT_REQUIRED");
        }
        FinanceRules.EnsureNotFuture(request.PaidOn, clock, "The payment date cannot be in the future.");
        // The front desk records cash and UPI; bank, cheque, card and wallet are finance's (doc §17.2).
        if (mode is not (InvoicePaymentMode.Cash or InvoicePaymentMode.Upi) && !currentUser.HasPermission("INVOICE.EDIT"))
        {
            throw new ForbiddenException("At the front desk you can record cash and UPI only. Ask finance to record this payment.");
        }

        return await FinanceRules.InTransactionAsync(db, async () =>
        {
            DateTime nowUtc = clock.UtcNow;
            Invoice invoice = await db.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct)
                ?? throw new NotFoundException("Invoice", invoiceId);
            if (invoice.Status == InvoiceStatus.Cancelled || invoice.Status == InvoiceStatus.Refunded)
            {
                throw new BusinessRuleException($"{invoice.InvoiceNumber} is {(invoice.Status == InvoiceStatus.Cancelled ? "cancelled" : "refunded")} and cannot take a payment.");
            }
            if (request.PaidOn < invoice.InvoiceDate)
            {
                throw new BusinessRuleException("The payment date cannot be before the invoice date.", "DATE_BEFORE_INVOICE");
            }
            await FinanceRules.EnsurePeriodOpenAsync(db, request.PaidOn, ct, "this payment");

            decimal outstanding = invoice.TotalAmount - invoice.PaidAmount;
            if (outstanding <= 0m)
            {
                throw new BusinessRuleException($"{invoice.InvoiceNumber} is already paid in full.");
            }
            if (amount > outstanding)
            {
                throw new BusinessRuleException($"This is more than the {FinanceRules.Money(outstanding)} outstanding. Reduce the amount, or add the extra to the customer's wallet separately.", "OVERPAYMENT");
            }
            if (request.BankAccountId != null && mode != InvoicePaymentMode.Cash && mode != InvoicePaymentMode.Wallet
                && !await db.BankAccounts.AnyAsync(a => a.BankAccountId == request.BankAccountId && a.IsActive, ct))
            {
                throw new BusinessRuleException("Choose which account the money went into.", "BANK_ACCOUNT_REQUIRED");
            }
            if (mode == InvoicePaymentMode.Wallet)
            {
                decimal balance = await db.Wallets.Where(w => w.CustomerId == invoice.CustomerId).Select(w => (decimal?)w.Balance).FirstOrDefaultAsync(ct) ?? 0m;
                if (balance <= 0m)
                {
                    throw new BusinessRuleException("No wallet balance.", "WALLET_EMPTY");
                }
                if (amount > balance)
                {
                    throw new BusinessRuleException($"The wallet only has {FinanceRules.Money(balance)}. Record {FinanceRules.Money(balance)} now and the rest will stay outstanding.", "WALLET_SHORT");
                }
            }

            DateTime paidUtc = request.PaidOn == FinanceRules.Today(clock) ? nowUtc : IstClock.ToUtc(request.PaidOn.ToDateTime(new TimeOnly(12, 0)));
            var payment = new Payment
            {
                PaymentNumber = await sequences.NextAsync(SequenceKeys.Payment, ct),
                CustomerId = invoice.CustomerId,
                BookingId = invoice.BookingId,
                CustomerPassId = invoice.CustomerPassId,
                InvoiceId = invoice.InvoiceId,
                Amount = amount,
                PaymentMethod = MethodFor(mode),
                Gateway = "Offline",
                Status = PaymentStatus.Success,
                PaidUtc = paidUtc
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);

            var receipt = new InvoicePayment
            {
                ReceiptNumber = await sequences.NextAsync(ReceiptSequence, ct),
                InvoiceId = invoice.InvoiceId,
                CustomerId = invoice.CustomerId,
                PaymentId = payment.PaymentId,
                Amount = amount,
                PaidOn = request.PaidOn,
                Mode = mode,
                Reference = mode == InvoicePaymentMode.Wallet ? null : reference,
                BankAccountId = mode is InvoicePaymentMode.Cash or InvoicePaymentMode.Wallet ? null : request.BankAccountId,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                RecordedByUserId = currentUser.UserId,
                RecordedUtc = nowUtc
            };

            if (mode == InvoicePaymentMode.Wallet)
            {
                await wallets.DebitAsync(invoice.CustomerId, amount, WalletCategory.AdminAdjustment,
                    $"Paid invoice {invoice.InvoiceNumber} from the wallet", "Invoice", invoice.InvoiceId, payment.PaymentId, ct);
            }
            // Cash on a booking or pass invoice goes into the collector's drawer. The Cash Collections
            // screens only know bookings and passes, so cash on a quotation invoice is receipted here only.
            if (mode == InvoicePaymentMode.Cash && (invoice.BookingId != null || invoice.CustomerPassId != null))
            {
                receipt.CashCollectionId = await WriteCashDrawerEntryAsync(invoice, payment, amount, reference, nowUtc, ct);
            }

            db.Set<InvoicePayment>().Add(receipt);
            await ApplyAsync(db, invoice, amount, nowUtc, ct);
            invoice.PaymentId ??= payment.PaymentId;

            decimal remaining = invoice.TotalAmount - invoice.PaidAmount;
            db.Notifications.Add(new Notification
            {
                UserId = invoice.CustomerId,
                Audience = NotificationAudience.SingleUser,
                Title = remaining <= 0m ? "Payment received" : "Part payment received",
                Message = $"{FinanceRules.Money(amount)} received against invoice {invoice.InvoiceNumber} ({ModeName(mode)}), receipt {receipt.ReceiptNumber}."
                          + (remaining > 0m ? $" {FinanceRules.Money(remaining)} is still due." : " The invoice is fully paid."),
                Category = NotificationCategory.Payment,
                Priority = NotificationPriority.Normal,
                ReferenceType = "Invoice",
                ReferenceId = invoice.InvoiceId,
                SentUtc = nowUtc
            });
            await db.SaveChangesAsync(ct);

            InvoicePaymentDto dto = await Project(db.Set<InvoicePayment>().AsNoTracking().Where(p => p.InvoicePaymentId == receipt.InvoicePaymentId)).FirstAsync(ct);
            string message = remaining <= 0m
                ? $"Payment of {FinanceRules.Money(amount)} recorded. {invoice.InvoiceNumber} is now Paid."
                : $"Payment of {FinanceRules.Money(amount)} recorded. {FinanceRules.Money(remaining)} still outstanding.";
            return new RecordInvoicePaymentResult(dto, invoice.InvoiceNumber, invoice.TotalAmount, invoice.PaidAmount, remaining, (byte)invoice.Status, message);
        }, ct);
    }

    /// <summary>Cash goes into the collector's cash-register session for the invoice's
    /// location, so Cash Register and Cash Collections stay correct.</summary>
    private async Task<long> WriteCashDrawerEntryAsync(Invoice invoice, Payment payment, decimal amount, string? reference, DateTime nowUtc, CancellationToken ct)
    {
        CashRegisterSession session = await CashRegisterService.OpenSessionForAsync(db, sequences, invoice.LocationId, currentUser.UserId, nowUtc, ct);
        var collection = new CashCollection
        {
            ReceiptNumber = await sequences.NextAsync(SequenceKeys.CashReceipt, ct),
            BookingId = invoice.BookingId,
            CustomerPassId = invoice.BookingId == null ? invoice.CustomerPassId : null,
            InvoiceId = invoice.InvoiceId,
            PaymentId = payment.PaymentId,
            CustomerId = invoice.CustomerId,
            LocationId = invoice.LocationId,
            PaymentMethod = PaymentMethod.Cash,
            Amount = amount,
            ReferenceNumber = reference,
            CollectedByUserId = currentUser.UserId,
            CollectedUtc = nowUtc,
            CashRegisterSessionId = session.CashRegisterSessionId,
            Status = CashCollectionStatus.Recorded,
            Notes = $"Invoice {invoice.InvoiceNumber}"
        };
        db.CashCollections.Add(collection);
        await db.SaveChangesAsync(ct);
        return collection.CashCollectionId;
    }

    /// <summary>Moves the invoice's paid amount and status; a pay-later booking or pass behind
    /// the invoice moves with it, so check-in opens once it is paid.</summary>
    public static async Task ApplyAsync(RegalisDbContext db, Invoice invoice, decimal delta, DateTime nowUtc, CancellationToken ct)
    {
        invoice.PaidAmount = Math.Max(0m, invoice.PaidAmount + delta);
        if (invoice.PaidAmount >= invoice.TotalAmount)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidUtc ??= nowUtc;
        }
        else
        {
            invoice.Status = invoice.PaidAmount > 0m ? InvoiceStatus.PartiallyPaid
                : invoice.Status == InvoiceStatus.Sent ? InvoiceStatus.Sent : InvoiceStatus.Pending;
            invoice.PaidUtc = null;
        }

        if (invoice.BookingId != null)
        {
            Booking? booking = await db.Bookings.FirstOrDefaultAsync(b => b.BookingId == invoice.BookingId, ct);
            if (booking != null && booking.IsPayLater)
            {
                booking.AmountPaid = Math.Min(booking.TotalAmount, Math.Max(0m, booking.AmountPaid + delta));
                booking.PaymentStatus = booking.AmountPaid >= booking.TotalAmount ? BookingPaymentStatus.Paid
                    : booking.AmountPaid > 0m ? BookingPaymentStatus.PartiallyPaid : BookingPaymentStatus.Pending;
            }
        }
        else if (invoice.CustomerPassId != null)
        {
            CustomerPass? pass = await db.CustomerPasses.FirstOrDefaultAsync(p => p.CustomerPassId == invoice.CustomerPassId, ct);
            if (pass != null && pass.IsPayLater)
            {
                pass.AmountPaid = Math.Min(pass.TotalAmount, Math.Max(0m, pass.AmountPaid + delta));
            }
        }
    }

    public async Task<InvoicePaymentDto> ReverseAsync(long paymentId, ReversePaymentRequest request, CancellationToken ct = default)
    {
        string reason = request.Reason?.Trim() ?? "";
        if (reason.Length < 3)
        {
            throw new BusinessRuleException("Say why this payment is being reversed.");
        }
        DateOnly today = FinanceRules.Today(clock);

        long reversalId = await FinanceRules.InTransactionAsync(db, async () =>
        {
            // Lets the cash-receipt guard accept the void below.
            using (BillingFinanceScope.Reverse())
            {
                DateTime nowUtc = clock.UtcNow;
                InvoicePayment original = await db.Set<InvoicePayment>()
                    .FirstOrDefaultAsync(p => p.PaymentId == paymentId && p.ReversalOfId == null, ct)
                    ?? throw new BusinessRuleException("Only a payment recorded against an invoice (cash, UPI, bank, cheque, card or wallet) can be reversed here. A gateway payment is refunded through the gateway.");
                if (original.IsReversed)
                {
                    throw new BusinessRuleException($"Receipt {original.ReceiptNumber} has already been reversed.");
                }
                if (original.BankTransactionId != null
                    || await db.BankTransactions.AnyAsync(t => t.MatchedPaymentId == paymentId, ct)
                    || await db.Set<BankMatchAllocation>().AnyAsync(m => m.DocType == BankMatchDocType.CustomerPayment && m.DocId == paymentId, ct))
                {
                    throw new BusinessRuleException("This payment is matched to a bank line. Unmatch it on Transactions first.");
                }
                await FinanceRules.EnsurePeriodOpenAsync(db, original.PaidOn, ct, "this reversal");
                await FinanceRules.EnsurePeriodOpenAsync(db, today, ct, "this reversal");

                Invoice invoice = await db.Invoices.FirstAsync(i => i.InvoiceId == original.InvoiceId, ct);
                Payment payment = await db.Payments.FirstAsync(p => p.PaymentId == paymentId, ct);

                var reversal = new InvoicePayment
                {
                    ReceiptNumber = await sequences.NextAsync(ReceiptSequence, ct),
                    InvoiceId = original.InvoiceId,
                    CustomerId = original.CustomerId,
                    Amount = -original.Amount,
                    PaidOn = today,
                    Mode = original.Mode,
                    Reference = $"Reversal of {original.ReceiptNumber}",
                    BankAccountId = original.BankAccountId,
                    Note = reason,
                    ReversalOfId = original.InvoicePaymentId,
                    RecordedByUserId = currentUser.UserId,
                    RecordedUtc = nowUtc
                };
                db.Set<InvoicePayment>().Add(reversal);
                original.IsReversed = true;
                original.ReversedByUserId = currentUser.UserId;
                original.ReversedUtc = nowUtc;
                original.ReverseReason = reason;

                payment.Status = PaymentStatus.Refunded;
                payment.RefundedAmount = payment.Amount;
                payment.RefundReference = $"Reversed: {reason}";

                if (original.Mode == InvoicePaymentMode.Wallet)
                {
                    await wallets.CreditAsync(original.CustomerId, original.Amount, WalletCategory.Refund,
                        $"Reversal of wallet payment on invoice {invoice.InvoiceNumber}", "Invoice", invoice.InvoiceId, payment.PaymentId, ct);
                }
                if (original.CashCollectionId != null)
                {
                    CashCollection? cash = await db.CashCollections.FirstOrDefaultAsync(c => c.CashCollectionId == original.CashCollectionId, ct);
                    if (cash != null && cash.Status == CashCollectionStatus.Recorded)
                    {
                        cash.Status = CashCollectionStatus.Voided;
                        cash.VoidedByUserId = currentUser.UserId;
                        cash.VoidedUtc = nowUtc;
                        cash.VoidReason = $"Payment reversed: {reason}";
                    }
                }

                await ApplyAsync(db, invoice, -original.Amount, nowUtc, ct);
                if (invoice.PaymentId == paymentId)
                {
                    invoice.PaymentId = null;
                }
                await db.SaveChangesAsync(ct);
                return reversal.InvoicePaymentId;
            }
        }, ct);

        return await Project(db.Set<InvoicePayment>().AsNoTracking().Where(p => p.InvoicePaymentId == reversalId)).FirstAsync(ct);
    }

    public async Task<PagedResult<PaymentRegisterRowDto>> GetRegisterAsync(PaymentRegisterRequest request, CancellationToken ct = default)
    {
        var query =
            from p in db.Payments.AsNoTracking()
            join ip in db.Set<InvoicePayment>().Where(x => x.ReversalOfId == null) on (long?)p.PaymentId equals ip.PaymentId into ipJoin
            from ip in ipJoin.DefaultIfEmpty()
            join c in db.Users on p.CustomerId equals c.UserId into cJoin
            from c in cJoin.DefaultIfEmpty()
            join i in db.Invoices on p.InvoiceId equals (long?)i.InvoiceId into iJoin
            from i in iJoin.DefaultIfEmpty()
            select new { p, ip, CustomerName = c != null ? c.FullName : "", InvoiceNumber = i != null ? i.InvoiceNumber : null };

        string source = (request.Source ?? "all").ToLowerInvariant();
        if (source == "online")
        {
            query = query.Where(x => x.ip == null && x.p.Gateway != null && x.p.Gateway != "Desk" && x.p.Gateway != "Offline");
        }
        else if (source == "offline")
        {
            query = query.Where(x => x.ip != null || x.p.Gateway == null || x.p.Gateway == "Desk" || x.p.Gateway == "Offline");
        }
        if (request.Status != null)
        {
            query = query.Where(x => (byte)x.p.Status == request.Status);
        }
        if (request.Mode != null)
        {
            query = query.Where(x => x.ip != null && (byte)x.ip.Mode == request.Mode);
        }
        if (request.From != null)
        {
            DateTime fromUtc = IstClock.ToUtc(request.From.Value.ToDateTime(TimeOnly.MinValue));
            query = query.Where(x => (x.p.PaidUtc ?? x.p.CreatedDate) >= fromUtc);
        }
        if (request.To != null)
        {
            DateTime toUtc = IstClock.ToUtc(request.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(x => (x.p.PaidUtc ?? x.p.CreatedDate) < toUtc);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string s = request.Search.Trim();
            query = query.Where(x => x.p.PaymentNumber.Contains(s) || (x.p.GatewayOrderId != null && x.p.GatewayOrderId.Contains(s))
                                     || x.CustomerName.Contains(s) || (x.InvoiceNumber != null && x.InvoiceNumber.Contains(s))
                                     || (x.ip != null && (x.ip.ReceiptNumber.Contains(s) || (x.ip.Reference != null && x.ip.Reference.Contains(s)))));
        }

        int total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.p.PaymentId)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new
            {
                x.p, x.ip, x.CustomerName, x.InvoiceNumber,
                Reconciled = db.BankTransactions.Any(t => t.MatchedPaymentId == x.p.PaymentId && t.ReconciliationStatus == ReconciliationStatus.Reconciled)
                             || db.Set<BankMatchAllocation>().Any(m => m.DocType == BankMatchDocType.CustomerPayment && m.DocId == x.p.PaymentId)
                             || (x.ip != null && x.ip.BankTransactionId != null)
            })
            .ToListAsync(ct);

        bool canReverse = currentUser.HasPermission("PAYMENT.REVERSE");
        var items = rows.Select(r =>
        {
            bool offline = r.ip != null || r.p.Gateway == null || r.p.Gateway == "Desk" || r.p.Gateway == "Offline";
            string modeName = r.ip != null ? ModeName(r.ip.Mode) : offline ? MethodName(r.p.PaymentMethod) : "Online · " + MethodName(r.p.PaymentMethod);
            string? reference = r.ip?.Reference ?? (offline ? r.p.GatewayPaymentId : r.p.GatewayPaymentId ?? r.p.GatewayOrderId);
            return new PaymentRegisterRowDto(r.p.PaymentId, r.p.PaymentNumber, r.p.CustomerId, r.CustomerName, r.p.Amount,
                (byte)r.p.PaymentMethod, (byte)r.p.Status, r.p.Gateway, r.p.GatewayOrderId, r.p.GatewayPaymentId, r.p.PaidUtc,
                r.p.CreatedDate, offline, modeName, reference, r.p.InvoiceId, r.InvoiceNumber, r.ip?.InvoicePaymentId,
                r.ip?.ReceiptNumber, r.ip?.IsReversed ?? false, r.ip?.ReverseReason,
                canReverse && r.ip != null && !r.ip.IsReversed && !r.Reconciled && r.p.Status == PaymentStatus.Success, r.Reconciled);
        }).ToList();
        return PagedResult<PaymentRegisterRowDto>.Create(items, request.Page, request.PageSize, total);
    }

    public async Task<string> EmailReceiptAsync(long paymentId, CancellationToken ct = default)
    {
        var p = await (from pay in db.Payments.AsNoTracking()
                       join u in db.Users on pay.CustomerId equals u.UserId
                       where pay.PaymentId == paymentId
                       select new { pay.PaymentNumber, pay.Status, u.Email, u.FullName }).FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Payment", paymentId);
        if (p.Status != PaymentStatus.Success)
        {
            throw new BusinessRuleException("Only a successful payment has a receipt.");
        }
        if (string.IsNullOrWhiteSpace(p.Email))
        {
            throw new BusinessRuleException("This customer has no email address on record.");
        }
        byte[] pdf = await payments.GetReceiptPdfAsync(paymentId, ct);
        QuotationDeliveryResult result = await dispatcher.SendEmailAsync(new QuotationEmailMessage(p.Email, null, null,
            $"Payment receipt {p.PaymentNumber} - Regalis WorkSpace",
            $"<p>Dear {System.Net.WebUtility.HtmlEncode(p.FullName)},</p><p>Thank you for your payment. Your receipt {p.PaymentNumber} is attached.</p><p>Regalis WorkSpace</p>",
            new[] { new QuotationOutboundFile($"{p.PaymentNumber}.pdf", "application/pdf", pdf) }), ct);
        if (!result.Succeeded)
        {
            throw new BusinessRuleException($"The receipt could not be emailed: {result.FailureReason}");
        }
        return result.Note ?? $"Receipt emailed to {p.Email}.";
    }

    public async Task<CustomerTaxProfileDto> GetCustomerTaxProfileAsync(long customerId, CancellationToken ct = default)
    {
        CustomerTaxProfile? row = await db.Set<CustomerTaxProfile>().AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == customerId, ct);
        return new CustomerTaxProfileDto(customerId, row?.Tan, row?.DoNotChase ?? false, row?.DoNotChaseReason);
    }

    public async Task<CustomerTaxProfileDto> SaveCustomerTaxProfileAsync(long customerId, SaveCustomerTaxProfileRequest request, CancellationToken ct = default)
    {
        string? tan = string.IsNullOrWhiteSpace(request.Tan) ? null : request.Tan.Trim().ToUpperInvariant();
        if (tan != null && !FinanceRules.IsTan(tan))
        {
            throw new BusinessRuleException("A TAN is four letters, five digits and one letter - for example BLRA12345B.");
        }
        if (request.DoNotChase && string.IsNullOrWhiteSpace(request.DoNotChaseReason))
        {
            throw new BusinessRuleException("Say why this customer should not be chased.");
        }
        if (!await db.Users.AnyAsync(u => u.UserId == customerId, ct))
        {
            throw new NotFoundException("Customer", customerId);
        }
        CustomerTaxProfile? row = await db.Set<CustomerTaxProfile>().FirstOrDefaultAsync(x => x.CustomerId == customerId, ct);
        if (row == null)
        {
            row = new CustomerTaxProfile { CustomerId = customerId };
            db.Set<CustomerTaxProfile>().Add(row);
        }
        row.Tan = tan;
        row.DoNotChase = request.DoNotChase;
        row.DoNotChaseReason = request.DoNotChase ? request.DoNotChaseReason!.Trim() : null;
        await db.SaveChangesAsync(ct);
        return new CustomerTaxProfileDto(customerId, row.Tan, row.DoNotChase, row.DoNotChaseReason);
    }
}
