using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Reporting;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Pay later at the front desk (decisions of 27 Oct 2026). Staff record each payment as
/// a numbered receipt: cash, UPI or card. A receipt is never edited or deleted; a mistake
/// is voided by an Admin, which keeps the row and reverses the totals it moved. Every
/// write runs in one serializable transaction with the booking's RowVersion, so two
/// staff cannot both take the last rupee of the same balance.
/// </summary>
public class CashCollectionService(
    RegalisDbContext db,
    ISettingService settings,
    INumberSequenceService sequences,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<CashCollectionService> logger) : ICashCollectionService
{
    private static readonly BookingStatus[] CollectableStatuses =
    {
        BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.Completed
    };

    public async Task<PagedResult<OutstandingPayLaterDto>> GetOutstandingAsync(OutstandingListRequest request, CancellationToken ct = default)
    {
        DateTime nowUtc = clock.UtcNow;
        IQueryable<Booking> query = db.Bookings.AsNoTracking()
            .Where(b => b.IsPayLater && CollectableStatuses.Contains(b.Status) && b.AmountPaid < b.TotalAmount);
        if (request.LocationId.HasValue)
        {
            query = query.Where(b => b.LocationId == request.LocationId.Value);
        }
        if (request.OverdueOnly)
        {
            query = query.Where(b => b.PaymentDueUtc != null && b.PaymentDueUtc < nowUtc);
        }
        if (request.From.HasValue)
        {
            query = query.Where(b => b.BookingDate >= request.From.Value);
        }
        if (request.To.HasValue)
        {
            query = query.Where(b => b.BookingDate <= request.To.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string s = request.Search.Trim();
            query = query.Where(b => b.BookingNumber.Contains(s) || b.Customer.FullName.Contains(s)
                                     || b.Customer.MobileNumber.Contains(s) || b.Customer.Email.Contains(s));
        }

        // Pay-later day passes and memberships with money still due (28 Oct 2026).
        IQueryable<CustomerPass> passes = db.CustomerPasses.AsNoTracking()
            .Where(p => p.IsPayLater && p.Status == CustomerPassStatus.Active && p.AmountPaid < p.TotalAmount);
        if (request.LocationId.HasValue)
        {
            passes = passes.Where(p => p.LocationId == request.LocationId.Value);
        }
        if (request.OverdueOnly)
        {
            passes = passes.Where(p => p.PaymentDueUtc != null && p.PaymentDueUtc < nowUtc);
        }
        if (request.From.HasValue)
        {
            passes = passes.Where(p => p.ValidFrom >= request.From.Value);
        }
        if (request.To.HasValue)
        {
            passes = passes.Where(p => p.ValidFrom <= request.To.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string s = request.Search.Trim();
            passes = passes.Where(p => p.PassNumber.Contains(s)
                || db.Users.Any(u => u.UserId == p.CustomerId && (u.FullName.Contains(s) || u.MobileNumber.Contains(s) || u.Email.Contains(s))));
        }

        // Both lists hold open balances only, so they are merged and paged in memory.
        var bookingRows = await query
            .Select(b => new
            {
                b.BookingId, b.BookingNumber, b.CustomerId, b.Customer.FullName, b.Customer.MobileNumber,
                b.LocationId, b.Workspace.WorkspaceName, b.StartUtc, b.EndUtc, b.BillingMonths, b.Status,
                b.TotalAmount, b.AmountPaid, b.PaymentDueUtc,
                Invoice = db.Invoices.Where(i => i.BookingId == b.BookingId && i.Status != InvoiceStatus.Cancelled)
                    .Select(i => new { i.InvoiceId, i.InvoiceNumber }).FirstOrDefault()
            })
            .ToListAsync(ct);
        var passRows = await passes
            .Select(p => new
            {
                p.CustomerPassId, p.PassNumber, p.CustomerId, p.LocationId, p.PassPlan.PlanName, p.ValidFrom, p.ValidTo,
                p.Status, p.TotalAmount, p.AmountPaid, p.PaymentDueUtc,
                Customer = db.Users.Where(u => u.UserId == p.CustomerId).Select(u => new { u.FullName, u.MobileNumber }).FirstOrDefault(),
                Invoice = db.Invoices.Where(i => i.CustomerPassId == p.CustomerPassId && i.Status != InvoiceStatus.Cancelled)
                    .Select(i => new { i.InvoiceId, i.InvoiceNumber }).FirstOrDefault()
            })
            .ToListAsync(ct);

        var all = bookingRows.Select(r => new OutstandingPayLaterDto(
                r.BookingId, r.BookingNumber, r.CustomerId, r.FullName, r.MobileNumber, r.LocationId, r.WorkspaceName,
                r.StartUtc, r.EndUtc, r.BillingMonths, (byte)r.Status, r.TotalAmount, r.AmountPaid,
                r.TotalAmount - r.AmountPaid, r.PaymentDueUtc, r.PaymentDueUtc.HasValue && r.PaymentDueUtc.Value < nowUtc,
                r.Invoice?.InvoiceId, r.Invoice?.InvoiceNumber))
            .Concat(passRows.Select(r => new OutstandingPayLaterDto(
                0, r.PassNumber, r.CustomerId, r.Customer?.FullName ?? "", r.Customer?.MobileNumber ?? "", r.LocationId, r.PlanName,
                IstClock.ToUtc(r.ValidFrom.ToDateTime(TimeOnly.MinValue)),
                IstClock.ToUtc(r.ValidTo.AddDays(1).ToDateTime(TimeOnly.MinValue)),
                null, (byte)r.Status, r.TotalAmount, r.AmountPaid, r.TotalAmount - r.AmountPaid, r.PaymentDueUtc,
                r.PaymentDueUtc.HasValue && r.PaymentDueUtc.Value < nowUtc,
                r.Invoice?.InvoiceId, r.Invoice?.InvoiceNumber, "Pass", r.CustomerPassId)))
            .OrderBy(r => r.PaymentDueUtc ?? r.StartUtc).ThenBy(r => r.BookingNumber)
            .ToList();
        int total = all.Count;
        var items = all.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return PagedResult<OutstandingPayLaterDto>.Create(items, request.Page, request.PageSize, total);
    }

    public async Task<CashCollectionDto> RecordAsync(RecordCollectionRequest request, CancellationToken ct = default)
    {
        if (request.CustomerPassId is > 0)
        {
            return await RecordForPassAsync(request, ct);
        }
        PaymentMethod method = (PaymentMethod)request.PaymentMethod;
        decimal amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        string? reference = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim();

        long collectionId = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            DateTime nowUtc = clock.UtcNow;

            Booking booking = await db.Bookings.Include(b => b.Workspace)
                .FirstOrDefaultAsync(b => b.BookingId == request.BookingId, ct)
                ?? throw new NotFoundException("Booking", request.BookingId ?? 0);
            if (!booking.IsPayLater)
            {
                throw new BusinessRuleException($"{booking.BookingNumber} was not booked with pay later.");
            }
            if (!CollectableStatuses.Contains(booking.Status))
            {
                throw new BusinessRuleException($"{booking.BookingNumber} is {booking.Status}; nothing can be collected on it.");
            }
            decimal balance = booking.TotalAmount - booking.AmountPaid;
            if (balance <= 0m)
            {
                throw new BusinessRuleException($"{booking.BookingNumber} is already paid in full.");
            }
            if (amount > balance)
            {
                throw new BusinessRuleException($"Only ₹{balance:N2} is due on {booking.BookingNumber}.");
            }

            CashRegisterSession session = await CashRegisterService.OpenSessionForAsync(
                db, sequences, booking.LocationId, currentUser.UserId, nowUtc, ct);

            Invoice? invoice = await db.Invoices
                .Where(i => i.BookingId == booking.BookingId && i.Status != InvoiceStatus.Cancelled)
                .OrderBy(i => i.InvoiceId)
                .FirstOrDefaultAsync(ct);

            var payment = new Payment
            {
                PaymentNumber = await sequences.NextAsync(SequenceKeys.Payment, ct),
                CustomerId = booking.CustomerId,
                BookingId = booking.BookingId,
                InvoiceId = invoice?.InvoiceId,
                Amount = amount,
                PaymentMethod = method,
                Status = PaymentStatus.Success,
                PaidUtc = nowUtc
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);

            decimal? tendered = method == PaymentMethod.Cash ? request.AmountTendered : null;
            var collection = new CashCollection
            {
                ReceiptNumber = await sequences.NextAsync(SequenceKeys.CashReceipt, ct),
                BookingId = booking.BookingId,
                InvoiceId = invoice?.InvoiceId,
                PaymentId = payment.PaymentId,
                CustomerId = booking.CustomerId,
                LocationId = booking.LocationId,
                PaymentMethod = method,
                Amount = amount,
                AmountTendered = tendered,
                ChangeReturned = tendered.HasValue ? tendered.Value - amount : null,
                ReferenceNumber = reference,
                CollectedByUserId = currentUser.UserId,
                CollectedUtc = nowUtc,
                CashRegisterSessionId = session.CashRegisterSessionId,
                Status = CashCollectionStatus.Recorded,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            };
            db.CashCollections.Add(collection);

            ApplyToBooking(booking, amount);
            if (invoice != null)
            {
                ApplyToInvoice(invoice, amount, nowUtc);
                invoice.PaymentId ??= payment.PaymentId;
            }

            decimal remaining = booking.TotalAmount - booking.AmountPaid;
            db.Notifications.Add(new Notification
            {
                UserId = booking.CustomerId,
                Audience = NotificationAudience.SingleUser,
                Title = remaining <= 0m ? "Payment received" : "Part payment received",
                Message = $"₹{amount:N2} received for {booking.BookingNumber} ({MethodName(method)}), receipt {collection.ReceiptNumber}."
                          + (remaining > 0m ? $" ₹{remaining:N2} is still due." : " Your booking is fully paid."),
                Category = NotificationCategory.Payment,
                Priority = NotificationPriority.Normal,
                ReferenceType = "Booking",
                ReferenceId = booking.BookingId,
                ActionLabel = "View booking",
                ActionRoute = $"/bookings/{booking.BookingId}",
                SentUtc = nowUtc
            });
            await AdminAlerts.BookingAsync(db, booking, "Pay later payment received", nowUtc, ct,
                $"₹{amount:N2} by {MethodName(method)}, receipt {collection.ReceiptNumber}." + (remaining > 0m ? $" ₹{remaining:N2} still due." : " Paid in full."));

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return collection.CashCollectionId;
        });

        logger.LogInformation("Desk collection {CollectionId} for booking {BookingId}: {Amount} by {Method}, user {UserId}",
            collectionId, request.BookingId, amount, method, currentUser.UserId);
        return await GetCollectionAsync(collectionId, ct);
    }

    /// <summary>A desk payment against a pay-later day pass or membership: same rules as a
    /// booking (never more than is due, one serializable transaction, a numbered receipt).</summary>
    private async Task<CashCollectionDto> RecordForPassAsync(RecordCollectionRequest request, CancellationToken ct)
    {
        PaymentMethod method = (PaymentMethod)request.PaymentMethod;
        decimal amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        string? reference = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim();
        long passId = request.CustomerPassId!.Value;

        long collectionId = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            DateTime nowUtc = clock.UtcNow;

            CustomerPass pass = await db.CustomerPasses.Include(p => p.PassPlan)
                .FirstOrDefaultAsync(p => p.CustomerPassId == passId, ct)
                ?? throw new NotFoundException("CustomerPass", passId);
            if (!pass.IsPayLater)
            {
                throw new BusinessRuleException($"{pass.PassNumber} was not bought with pay later.");
            }
            if (pass.Status != CustomerPassStatus.Active)
            {
                throw new BusinessRuleException($"{pass.PassNumber} is {pass.Status}; nothing can be collected on it.");
            }
            decimal balance = pass.TotalAmount - pass.AmountPaid;
            if (balance <= 0m)
            {
                throw new BusinessRuleException($"{pass.PassNumber} is already paid in full.");
            }
            if (amount > balance)
            {
                throw new BusinessRuleException($"Only ₹{balance:N2} is due on {pass.PassNumber}.");
            }

            CashRegisterSession session = await CashRegisterService.OpenSessionForAsync(
                db, sequences, pass.LocationId, currentUser.UserId, nowUtc, ct);

            Invoice? invoice = await db.Invoices
                .Where(i => i.CustomerPassId == pass.CustomerPassId && i.Status != InvoiceStatus.Cancelled)
                .OrderBy(i => i.InvoiceId)
                .FirstOrDefaultAsync(ct);

            var payment = new Payment
            {
                PaymentNumber = await sequences.NextAsync(SequenceKeys.Payment, ct),
                CustomerId = pass.CustomerId,
                CustomerPassId = pass.CustomerPassId,
                InvoiceId = invoice?.InvoiceId,
                Amount = amount,
                PaymentMethod = method,
                Status = PaymentStatus.Success,
                PaidUtc = nowUtc
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);

            decimal? tendered = method == PaymentMethod.Cash ? request.AmountTendered : null;
            var collection = new CashCollection
            {
                ReceiptNumber = await sequences.NextAsync(SequenceKeys.CashReceipt, ct),
                CustomerPassId = pass.CustomerPassId,
                InvoiceId = invoice?.InvoiceId,
                PaymentId = payment.PaymentId,
                CustomerId = pass.CustomerId,
                LocationId = pass.LocationId,
                PaymentMethod = method,
                Amount = amount,
                AmountTendered = tendered,
                ChangeReturned = tendered.HasValue ? tendered.Value - amount : null,
                ReferenceNumber = reference,
                CollectedByUserId = currentUser.UserId,
                CollectedUtc = nowUtc,
                CashRegisterSessionId = session.CashRegisterSessionId,
                Status = CashCollectionStatus.Recorded,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            };
            db.CashCollections.Add(collection);

            ApplyToPass(pass, amount);
            if (invoice != null)
            {
                ApplyToInvoice(invoice, amount, nowUtc);
                invoice.PaymentId ??= payment.PaymentId;
            }
            pass.PaymentId ??= payment.PaymentId;

            decimal remaining = pass.TotalAmount - pass.AmountPaid;
            db.Notifications.Add(new Notification
            {
                UserId = pass.CustomerId,
                Audience = NotificationAudience.SingleUser,
                Title = remaining <= 0m ? "Payment received" : "Part payment received",
                Message = $"₹{amount:N2} received for {pass.PassPlan.PlanName} ({pass.PassNumber}) ({MethodName(method)}), receipt {collection.ReceiptNumber}."
                          + (remaining > 0m ? $" ₹{remaining:N2} is still due." : " Your pass is fully paid and your QR now opens the door."),
                Category = NotificationCategory.Payment,
                Priority = NotificationPriority.Normal,
                ReferenceType = "CustomerPass",
                ReferenceId = pass.CustomerPassId,
                ActionLabel = "View passes",
                ActionRoute = "/passes/my",
                SentUtc = nowUtc
            });
            AdminAlerts.Raise(db, new AdminEvent(AdminNotificationType.Payment, "paylater.pass_paid",
                $"Pay later payment received — {pass.PassNumber}",
                $"₹{amount:N2} by {MethodName(method)}, receipt {collection.ReceiptNumber}." + (remaining > 0m ? $" ₹{remaining:N2} still due." : " Paid in full."),
                AdminNotificationSeverity.Info, pass.CustomerId, "CustomerPass", pass.CustomerPassId, pass.PassNumber,
                "Open Cash Collections", "/admin/billing/cash-collections"), nowUtc);

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return collection.CashCollectionId;
        });

        logger.LogInformation("Desk collection {CollectionId} for pass {PassId}: {Amount} by {Method}, user {UserId}",
            collectionId, passId, amount, method, currentUser.UserId);
        return await GetCollectionAsync(collectionId, ct);
    }

    public async Task<CashCollectionDto> VoidAsync(long cashCollectionId, VoidCollectionRequest request, CancellationToken ct = default)
    {
        string reason = request.Reason.Trim();
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            DateTime nowUtc = clock.UtcNow;

            CashCollection collection = await db.CashCollections.FirstOrDefaultAsync(c => c.CashCollectionId == cashCollectionId, ct)
                ?? throw new NotFoundException("CashCollection", cashCollectionId);
            if (collection.Status == CashCollectionStatus.Voided)
            {
                throw new BusinessRuleException($"Receipt {collection.ReceiptNumber} is already voided.");
            }

            Booking? booking = collection.BookingId is null ? null
                : await db.Bookings.FirstAsync(b => b.BookingId == collection.BookingId, ct);
            CustomerPass? pass = collection.CustomerPassId is null ? null
                : await db.CustomerPasses.FirstAsync(p => p.CustomerPassId == collection.CustomerPassId, ct);
            collection.Status = CashCollectionStatus.Voided;
            collection.VoidedByUserId = currentUser.UserId;
            collection.VoidedUtc = nowUtc;
            collection.VoidReason = reason;

            if (booking is not null)
            {
                ApplyToBooking(booking, -collection.Amount);
            }
            if (pass is not null)
            {
                ApplyToPass(pass, -collection.Amount);
            }
            if (collection.InvoiceId.HasValue)
            {
                Invoice invoice = await db.Invoices.FirstAsync(i => i.InvoiceId == collection.InvoiceId.Value, ct);
                ApplyToInvoice(invoice, -collection.Amount, nowUtc);
                if (invoice.PaymentId == collection.PaymentId)
                {
                    invoice.PaymentId = await db.CashCollections
                        .Where(c => c.InvoiceId == invoice.InvoiceId && c.Status == CashCollectionStatus.Recorded
                                    && c.CashCollectionId != collection.CashCollectionId)
                        .OrderBy(c => c.CashCollectionId).Select(c => c.PaymentId).FirstOrDefaultAsync(ct);
                }
            }
            if (collection.PaymentId.HasValue)
            {
                Payment payment = await db.Payments.FirstAsync(p => p.PaymentId == collection.PaymentId.Value, ct);
                payment.Status = PaymentStatus.Cancelled;
                payment.FailureReason = $"Receipt {collection.ReceiptNumber} voided: {reason}";
            }

            if (booking is not null)
            {
                await AdminAlerts.BookingAsync(db, booking, "Pay later receipt voided", nowUtc, ct,
                    $"Receipt {collection.ReceiptNumber} (₹{collection.Amount:N2}) voided: {reason}");
            }
            else if (pass is not null)
            {
                AdminAlerts.Raise(db, new AdminEvent(AdminNotificationType.Payment, "paylater.pass_void",
                    $"Pay later receipt voided — {pass.PassNumber}",
                    $"Receipt {collection.ReceiptNumber} (₹{collection.Amount:N2}) voided: {reason}",
                    AdminNotificationSeverity.Info, pass.CustomerId, "CustomerPass", pass.CustomerPassId, pass.PassNumber,
                    "Open Cash Collections", "/admin/billing/cash-collections"), nowUtc);
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

        logger.LogWarning("Desk collection {CollectionId} voided by {UserId}: {Reason}", cashCollectionId, currentUser.UserId, reason);
        return await GetCollectionAsync(cashCollectionId, ct);
    }

    public async Task<PagedResult<CashCollectionDto>> GetCollectionsAsync(CashCollectionListRequest request, CancellationToken ct = default)
    {
        IQueryable<CashCollection> query = db.CashCollections.AsNoTracking();
        if (request.LocationId.HasValue)
        {
            query = query.Where(c => c.LocationId == request.LocationId.Value);
        }
        if (request.CollectedByUserId.HasValue)
        {
            query = query.Where(c => c.CollectedByUserId == request.CollectedByUserId.Value);
        }
        if (request.BookingId.HasValue)
        {
            query = query.Where(c => c.BookingId == request.BookingId.Value);
        }
        if (request.CustomerPassId.HasValue)
        {
            query = query.Where(c => c.CustomerPassId == request.CustomerPassId.Value);
        }
        if (request.CashRegisterSessionId.HasValue)
        {
            query = query.Where(c => c.CashRegisterSessionId == request.CashRegisterSessionId.Value);
        }
        if (request.PaymentMethod.HasValue)
        {
            var m = (PaymentMethod)request.PaymentMethod.Value;
            query = query.Where(c => c.PaymentMethod == m);
        }
        if (request.Status.HasValue)
        {
            var st = (CashCollectionStatus)request.Status.Value;
            query = query.Where(c => c.Status == st);
        }
        if (request.From.HasValue)
        {
            DateTime fromUtc = IstClock.ToUtc(request.From.Value.ToDateTime(TimeOnly.MinValue));
            query = query.Where(c => c.CollectedUtc >= fromUtc);
        }
        if (request.To.HasValue)
        {
            DateTime toUtc = IstClock.ToUtc(request.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(c => c.CollectedUtc < toUtc);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string s = request.Search.Trim();
            query = query.Where(c => c.ReceiptNumber.Contains(s)
                                     || (c.Booking != null && c.Booking.BookingNumber.Contains(s))
                                     || (c.CustomerPass != null && c.CustomerPass.PassNumber.Contains(s))
                                     || db.Users.Any(u => u.UserId == c.CustomerId && u.FullName.Contains(s))
                                     || (c.ReferenceNumber != null && c.ReferenceNumber.Contains(s)));
        }

        int total = await query.CountAsync(ct);
        List<CashCollectionDto> items = await Project(request.IsDescending
                ? query.OrderByDescending(c => c.CollectedUtc).ThenByDescending(c => c.CashCollectionId)
                : query.OrderBy(c => c.CollectedUtc).ThenBy(c => c.CashCollectionId))
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(ct);
        return PagedResult<CashCollectionDto>.Create(items, request.Page, request.PageSize, total);
    }

    public async Task<CashCollectionDto> GetCollectionAsync(long cashCollectionId, CancellationToken ct = default)
    {
        return await Project(db.CashCollections.AsNoTracking().Where(c => c.CashCollectionId == cashCollectionId))
                   .FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException("CashCollection", cashCollectionId);
    }

    public async Task<(byte[] Content, string FileName)> GetReceiptPdfAsync(long cashCollectionId, CancellationToken ct = default)
    {
        CashCollectionDto c = await GetCollectionAsync(cashCollectionId, ct);
        var booking = c.CustomerPassId is not null
            ? await PassReceiptFactsAsync(c.CustomerPassId.Value, ct)
            : await db.Bookings.AsNoTracking().Where(b => b.BookingId == c.BookingId)
                .Select(b => new ReceiptFacts(b.TotalAmount, b.AmountPaid, b.StartUtc, b.EndUtc, b.Workspace.WorkspaceName, b.Customer.MobileNumber))
                .FirstAsync(ct);
        var receipt = new CashReceiptPdfGenerator.Receipt(
            c.ReceiptNumber, c.CollectedUtc, c.CustomerName, booking.MobileNumber, c.BookingNumber, booking.WorkspaceName,
            booking.StartUtc, booking.EndUtc, c.InvoiceNumber, c.PaymentNumber, MethodName((PaymentMethod)c.PaymentMethod),
            c.Amount, c.AmountTendered, c.ChangeReturned, c.ReferenceNumber, booking.TotalAmount, booking.AmountPaid,
            c.CollectedByName, c.Status == (byte)CashCollectionStatus.Voided, c.VoidReason,
            await settings.GetStringAsync("Company.Name", "Regalis WorkSpace", ct),
            await settings.GetStringAsync("Company.Address", "", ct),
            await settings.GetStringAsync("Company.Gstin", "", ct));
        byte[] pdf = CashReceiptPdfGenerator.Generate(receipt, await PdfLogo.LoadAsync(settings, ct));
        return (pdf, $"{c.ReceiptNumber.Replace('/', '-')}.pdf");
    }

    /// <summary>Moves the booking's running total and payment status by a receipt (or,
    /// negative, by a void).</summary>
    internal static void ApplyToBooking(Booking booking, decimal delta)
    {
        booking.AmountPaid = Math.Max(0m, booking.AmountPaid + delta);
        booking.PaymentStatus = booking.AmountPaid >= booking.TotalAmount
            ? BookingPaymentStatus.Paid
            : booking.AmountPaid > 0m ? BookingPaymentStatus.PartiallyPaid : BookingPaymentStatus.Pending;
    }

    /// <summary>Moves a pay-later pass's running total by a receipt (or, negative, a void).</summary>
    internal static void ApplyToPass(CustomerPass pass, decimal delta)
    {
        pass.AmountPaid = Math.Min(pass.TotalAmount, Math.Max(0m, pass.AmountPaid + delta));
    }

    private sealed record ReceiptFacts(decimal TotalAmount, decimal AmountPaid, DateTime StartUtc, DateTime EndUtc,
        string WorkspaceName, string MobileNumber);

    private async Task<ReceiptFacts> PassReceiptFactsAsync(long customerPassId, CancellationToken ct)
    {
        var p = await db.CustomerPasses.AsNoTracking().Where(x => x.CustomerPassId == customerPassId)
            .Select(x => new { x.TotalAmount, x.AmountPaid, x.ValidFrom, x.ValidTo, x.PassPlan.PlanName, x.CustomerId })
            .FirstAsync(ct);
        string mobile = await db.Users.Where(u => u.UserId == p.CustomerId).Select(u => u.MobileNumber).FirstOrDefaultAsync(ct) ?? "";
        return new ReceiptFacts(p.TotalAmount, p.AmountPaid,
            IstClock.ToUtc(p.ValidFrom.ToDateTime(TimeOnly.MinValue)),
            IstClock.ToUtc(p.ValidTo.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            p.PlanName, mobile);
    }

    internal static void ApplyToInvoice(Invoice invoice, decimal delta, DateTime nowUtc)
    {
        invoice.PaidAmount = Math.Max(0m, invoice.PaidAmount + delta);
        if (invoice.PaidAmount >= invoice.TotalAmount)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidUtc ??= nowUtc;
        }
        else
        {
            invoice.Status = invoice.PaidAmount > 0m ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Pending;
            invoice.PaidUtc = null;
        }
    }

    internal static string MethodName(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "cash",
        PaymentMethod.Upi => "UPI",
        PaymentMethod.CreditCard => "credit card",
        PaymentMethod.DebitCard => "debit card",
        _ => method.ToString()
    };

    private IQueryable<CashCollectionDto> Project(IQueryable<CashCollection> query)
    {
        return query.Select(c => new CashCollectionDto(
            c.CashCollectionId,
            c.ReceiptNumber,
            c.BookingId ?? 0,
            c.Booking != null ? c.Booking.BookingNumber : c.CustomerPass!.PassNumber,
            c.CustomerId,
            db.Users.Where(u => u.UserId == c.CustomerId).Select(u => u.FullName).FirstOrDefault() ?? "",
            c.LocationId,
            c.InvoiceId,
            db.Invoices.Where(i => i.InvoiceId == c.InvoiceId).Select(i => i.InvoiceNumber).FirstOrDefault(),
            c.PaymentId,
            db.Payments.Where(p => p.PaymentId == c.PaymentId).Select(p => p.PaymentNumber).FirstOrDefault(),
            (byte)c.PaymentMethod,
            c.Amount,
            c.AmountTendered,
            c.ChangeReturned,
            c.ReferenceNumber,
            c.CollectedByUserId,
            db.Users.Where(u => u.UserId == c.CollectedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "",
            c.CollectedUtc,
            c.CashRegisterSessionId,
            c.CashRegisterSession != null ? c.CashRegisterSession.SessionNumber : null,
            (byte)c.Status,
            db.Users.Where(u => u.UserId == c.VoidedByUserId).Select(u => u.FullName).FirstOrDefault(),
            c.VoidedUtc,
            c.VoidReason,
            c.Notes,
            c.Booking != null ? c.Booking.TotalAmount - c.Booking.AmountPaid : c.CustomerPass!.TotalAmount - c.CustomerPass.AmountPaid,
            c.CustomerPassId));
    }
}
