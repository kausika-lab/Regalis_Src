using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Bookings;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Domain.Entities.Access;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Post-confirmation booking changes. Cancellation never deletes (Invariant 3):
/// it is a status change + history + policy refund to the wallet. Reschedule and
/// extend re-run the locked availability check like any other occupancy write.
/// </summary>
public class BookingMutationService(
    RegalisDbContext db,
    ISettingService settings,
    INumberSequenceService sequences,
    IWalletService walletService,
    IBookingService bookingService,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<BookingMutationService> logger) : IBookingMutationService
{
    public async Task<CancelBookingResponse> CancelAsync(long bookingId,
        CancelBookingRequest request, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        var (refund, percent) = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var booking = await LoadOwnedBookingAsync(bookingId, ct);
            if (booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
            {
                throw new BusinessRuleException(
                    "Only pending or confirmed bookings can be cancelled.");
            }
            var nowUtc = clock.UtcNow;
            if (booking.StartUtc <= nowUtc)
            {
                throw new BusinessRuleException("The booking has already started.");
            }

            decimal refundAmount = 0;
            decimal refundPercent = 0;
            if (booking.Status == BookingStatus.Confirmed &&
                booking.PaymentStatus == BookingPaymentStatus.Paid)
            {
                var cutoffHours = await settings.GetIntAsync(
                    AppSettingKeys.Cancellation.CutoffHours, 24, ct);
                var beforeCutoff = booking.StartUtc - nowUtc >= TimeSpan.FromHours(cutoffHours);
                refundPercent = beforeCutoff
                    ? await settings.GetDecimalAsync(
                        AppSettingKeys.Cancellation.RefundPercentBeforeCutoff, 100m, ct)
                    : await settings.GetDecimalAsync(
                        AppSettingKeys.Cancellation.RefundPercentAfterCutoff, 0m, ct);
                refundAmount = Math.Round(booking.TotalAmount * refundPercent / 100m, 2,
                    MidpointRounding.AwayFromZero);
                if (refundAmount > 0)
                {
                    // Refunds credit the personal wallet (A15) — never the gateway.
                    await walletService.CreditAsync(booking.CustomerId, refundAmount,
                        WalletCategory.Refund,
                        $"Refund {refundPercent:0.##}% for cancelled booking {booking.BookingNumber}",
                        referenceType: "Booking", referenceId: booking.BookingId, ct: ct);
                }
                booking.RefundAmount = refundAmount;
                booking.PaymentStatus = refundAmount >= booking.TotalAmount
                    ? BookingPaymentStatus.Refunded
                    : refundAmount > 0
                        ? BookingPaymentStatus.PartiallyRefunded
                        : booking.PaymentStatus;
            }

            var previousStatus = booking.Status;
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledUtc = nowUtc;
            booking.CancelledByUserId = currentUser.UserId;
            booking.CancellationReason = request.Reason?.Trim();
            booking.StatusHistory.Add(new BookingStatusHistory
            {
                BookingId = booking.BookingId,
                FromStatus = previousStatus,
                ToStatus = BookingStatus.Cancelled,
                ChangedByUserId = currentUser.UserId,
                ChangedUtc = nowUtc,
                Reason = request.Reason?.Trim() ??
                         $"Cancelled; refund {refundPercent:0.##}% = {refundAmount:0.00}"
            });

            await RevokeAccessTokensAsync(booking.BookingId, "BookingCancelled", nowUtc, ct);
            db.Notifications.Add(BuildNotification(booking,
                "Booking cancelled",
                $"{booking.BookingNumber} has been cancelled." +
                (refundAmount > 0 ? $" ₹{refundAmount:N2} was refunded to your wallet." : ""),
                nowUtc));
            await AdminAlerts.BookingAsync(db, booking, "Booking cancelled", nowUtc, ct,
                refundAmount > 0 ? $"₹{refundAmount:N2} refunded to the wallet." : null);

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (refundAmount, refundPercent);
        });

        logger.LogInformation("Booking {BookingId} cancelled by {UserId}; refund {Refund}",
            bookingId, currentUser.UserId, refund);
        var dto = await bookingService.GetBookingAsync(bookingId, ct);
        return new CancelBookingResponse(dto, refund, percent, refund > 0);
    }

    public async Task<BookingDto> RescheduleAsync(long bookingId,
        RescheduleBookingRequest request, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction =
                await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var booking = await LoadOwnedBookingAsync(bookingId, ct);
            if (booking.Status != BookingStatus.Confirmed)
            {
                throw new BusinessRuleException("Only confirmed bookings can be rescheduled.");
            }
            if (booking.BillingMonths is not null)
            {
                throw new BusinessRuleException(
                    "A monthly desk or cabin booking cannot be rescheduled. Cancel it and book a new term instead.");
            }
            var nowUtc = clock.UtcNow;
            if (booking.StartUtc <= nowUtc)
            {
                throw new BusinessRuleException("The booking has already started.");
            }

            var newStartUtc = request.NewStartUtc;
            var newEndUtc = newStartUtc.AddMinutes(booking.DurationMinutes);
            await ValidateWindowAsync(booking.WorkspaceId, newStartUtc, booking.DurationMinutes, ct);

            var conflicts = await BookingEngineLocks.CountConflictsAsync(
                db, booking.WorkspaceId, newStartUtc, newEndUtc, booking.BookingId, ct);
            if (conflicts > 0)
            {
                throw new ConflictException("The new slot is not available.");
            }

            var oldStartIst = IstClock.ToIst(booking.StartUtc);
            booking.StartUtc = newStartUtc;
            booking.EndUtc = newEndUtc;
            booking.BookingDate = IstClock.IstDateOf(newStartUtc);
            booking.StatusHistory.Add(new BookingStatusHistory
            {
                BookingId = booking.BookingId,
                FromStatus = BookingStatus.Confirmed,
                ToStatus = BookingStatus.Confirmed,
                ChangedByUserId = currentUser.UserId,
                ChangedUtc = nowUtc,
                Reason = $"Rescheduled from {oldStartIst:dd MMM HH:mm} IST to " +
                         $"{IstClock.ToIst(newStartUtc):dd MMM HH:mm} IST."
            });

            // The QR pass follows the booking window.
            var tokens = await db.AccessTokens
                .Where(t => t.BookingId == booking.BookingId && t.Status == AccessTokenStatus.Active)
                .ToListAsync(ct);
            foreach (var token in tokens)
            {
                token.ValidFromUtc = newStartUtc;
                token.ValidToUtc = newEndUtc;
            }

            db.Notifications.Add(BuildNotification(booking, "Booking rescheduled",
                $"{booking.BookingNumber} now runs {IstClock.ToIst(newStartUtc):dd MMM yyyy HH:mm}–" +
                $"{IstClock.ToIst(newEndUtc):HH:mm} IST.", nowUtc));
            await AdminAlerts.BookingAsync(db, booking, "Booking rescheduled", nowUtc, ct,
                $"Moved from {oldStartIst:dd MMM, HH:mm}.");

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        });

        return await bookingService.GetBookingAsync(bookingId, ct);
    }

    public async Task<ExtendBookingResponse> ExtendAsync(long bookingId,
        ExtendBookingRequest request, CancellationToken ct = default)
    {
        if (request.PaymentMethod == PaymentMethods.Cash && currentUser.UserType == UserType.Customer)
        {
            throw new ForbiddenException("Cash settlement is available at the front desk only.");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        var (additionalAmount, invoiceId, invoiceNumber) = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction =
                await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var booking = await db.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Workspace).ThenInclude(w => w.Location)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct)
                ?? throw new NotFoundException("Booking", bookingId);
            EnsureOwnership(booking);
            if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.CheckedIn))
            {
                throw new BusinessRuleException("Only confirmed or checked-in bookings can be extended.");
            }
            if (booking.BillingMonths is not null)
            {
                throw new BusinessRuleException(
                    "A monthly desk or cabin booking cannot be extended. Book the next term when this one ends.");
            }
            var nowUtc = clock.UtcNow;
            if (booking.EndUtc <= nowUtc)
            {
                throw new BusinessRuleException("The booking has already ended.");
            }

            var granularity = await settings.GetIntAsync(
                AppSettingKeys.Booking.SlotGranularityMinutes, 30, ct);
            var maxMinutes = await settings.GetIntAsync(AppSettingKeys.Booking.MaxMinutes, 480, ct);
            if (request.AdditionalMinutes % granularity != 0)
            {
                throw new BusinessRuleException(
                    $"The extension must be a multiple of {granularity} minutes.");
            }
            if (booking.DurationMinutes + request.AdditionalMinutes > maxMinutes)
            {
                throw new BusinessRuleException(
                    $"A booking cannot exceed {maxMinutes} minutes in total.");
            }

            var oldEndUtc = booking.EndUtc;
            var newEndUtc = oldEndUtc.AddMinutes(request.AdditionalMinutes);
            var location = booking.Workspace.Location;
            var closeIst = IstClock.ToIst(booking.StartUtc).Date + location.WorkingHoursEnd.ToTimeSpan();
            if (IstClock.ToIst(newEndUtc) > closeIst)
            {
                throw new BusinessRuleException(
                    $"The extension runs past closing ({location.WorkingHoursEnd:HH\\:mm} IST).");
            }

            var conflicts = await BookingEngineLocks.CountConflictsAsync(
                db, booking.WorkspaceId, oldEndUtc, newEndUtc, booking.BookingId, ct);
            if (conflicts > 0)
            {
                throw new ConflictException("The workspace is taken right after your booking.");
            }

            // Price the extra minutes the same way the original was priced.
            var cgstPercent = await settings.GetDecimalAsync(AppSettingKeys.Tax.CgstPercent, 9m, ct);
            var sgstPercent = await settings.GetDecimalAsync(AppSettingKeys.Tax.SgstPercent, 9m, ct);
            var extraBase = Math.Round(
                booking.Workspace.PricePerHour * request.AdditionalMinutes / 60m, 2,
                MidpointRounding.AwayFromZero);
            var extraCgst = Math.Round(extraBase * cgstPercent / 100m, 2, MidpointRounding.AwayFromZero);
            var extraSgst = Math.Round(extraBase * sgstPercent / 100m, 2, MidpointRounding.AwayFromZero);
            var extraTotal = extraBase + extraCgst + extraSgst;

            Payment payment;
            if (request.PaymentMethod == PaymentMethods.Wallet)
            {
                await walletService.DebitAsync(booking.CustomerId, extraTotal,
                    WalletCategory.Booking,
                    $"Extension of booking {booking.BookingNumber} (+{request.AdditionalMinutes} min)",
                    referenceType: "Booking", referenceId: booking.BookingId, ct: ct);
                payment = NewPayment(booking, extraTotal, PaymentMethod.Wallet, nowUtc);
            }
            else
            {
                payment = NewPayment(booking, extraTotal, PaymentMethod.Cash, nowUtc);
            }
            payment.PaymentNumber = await sequences.NextAsync(SequenceKeys.Payment, ct);
            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);

            // Supplementary GST invoice for the extension.
            var invoiceDate = IstClock.IstDateOf(nowUtc);
            var invoice = new Invoice
            {
                InvoiceNumber = await sequences.NextAsync(SequenceKeys.Invoice, ct),
                CustomerId = booking.CustomerId,
                BookingId = booking.BookingId,
                LocationId = booking.LocationId,
                InvoiceDate = invoiceDate,
                DueDate = invoiceDate,
                SubTotal = extraBase,
                TaxableAmount = extraBase,
                CgstPercent = cgstPercent,
                SgstPercent = sgstPercent,
                CgstAmount = extraCgst,
                SgstAmount = extraSgst,
                TotalAmount = extraTotal,
                PaidAmount = extraTotal,
                Status = InvoiceStatus.Paid,
                PaymentId = payment.PaymentId,
                PaidUtc = nowUtc,
                CustomerName = booking.Customer.FullName,
                CustomerEmail = booking.Customer.Email,
                CustomerMobile = booking.Customer.MobileNumber,
                CustomerGstin = booking.Customer.Gstin,
                CustomerCompanyName = booking.Customer.CompanyName,
                BillingAddress = booking.Customer.Address,
                CompanyName = await settings.GetStringAsync(AppSettingKeys.Company.Name, "Regalis WorkSpace", ct),
                CompanyGstin = await settings.GetStringAsync(AppSettingKeys.Company.Gstin, "", ct),
                CompanyAddress = await settings.GetStringAsync(AppSettingKeys.Company.Address, "", ct)
            };
            invoice.Lines.Add(new InvoiceLine
            {
                LineNumber = 1,
                Description = $"Extension of booking {booking.BookingNumber} — " +
                              $"+{request.AdditionalMinutes} min on {booking.Workspace.WorkspaceName}",
                Quantity = Math.Round(request.AdditionalMinutes / 60m, 3),
                UnitPrice = booking.Workspace.PricePerHour,
                LineAmount = extraBase,
                TaxPercent = cgstPercent + sgstPercent,
                TaxAmount = extraCgst + extraSgst,
                TotalAmount = extraTotal
            });
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(ct);
            payment.InvoiceId = invoice.InvoiceId;

            booking.EndUtc = newEndUtc;
            booking.DurationMinutes += request.AdditionalMinutes;
            booking.BaseAmount += extraBase;
            booking.TaxAmount += extraCgst + extraSgst;
            booking.TotalAmount += extraTotal;
            booking.StatusHistory.Add(new BookingStatusHistory
            {
                BookingId = booking.BookingId,
                FromStatus = booking.Status,
                ToStatus = booking.Status,
                ChangedByUserId = currentUser.UserId,
                ChangedUtc = nowUtc,
                Reason = $"Extended by {request.AdditionalMinutes} min; " +
                         $"invoice {invoice.InvoiceNumber} for ₹{extraTotal:N2}."
            });

            var tokens = await db.AccessTokens
                .Where(t => t.BookingId == booking.BookingId && t.Status == AccessTokenStatus.Active)
                .ToListAsync(ct);
            foreach (var token in tokens)
            {
                token.ValidToUtc = newEndUtc;
            }

            db.Notifications.Add(BuildNotification(booking, "Booking extended",
                $"{booking.BookingNumber} now ends at {IstClock.ToIst(newEndUtc):HH:mm} IST.", nowUtc));

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (extraTotal, invoice.InvoiceId, invoice.InvoiceNumber);
        });

        var dto = await bookingService.GetBookingAsync(bookingId, ct);
        return new ExtendBookingResponse(dto, additionalAmount, invoiceId, invoiceNumber);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<Booking> LoadOwnedBookingAsync(long bookingId, CancellationToken ct)
    {
        var booking = await db.Bookings
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct)
            ?? throw new NotFoundException("Booking", bookingId);
        EnsureOwnership(booking);
        return booking;
    }

    private void EnsureOwnership(Booking booking)
    {
        if (currentUser.UserType == UserType.Customer && booking.CustomerId != currentUser.UserId)
        {
            throw new NotFoundException("Booking", booking.BookingId);
        }
    }

    /// <summary>Reschedule window rules: future start, aligned, inside working hours.</summary>
    private async Task ValidateWindowAsync(int workspaceId, DateTime startUtc, int durationMinutes,
        CancellationToken ct)
    {
        var workspace = await db.Workspaces.AsNoTracking()
            .Include(w => w.Location)
            .FirstAsync(w => w.WorkspaceId == workspaceId, ct);
        var nowUtc = clock.UtcNow;
        var granularity = await settings.GetIntAsync(AppSettingKeys.Booking.SlotGranularityMinutes, 30, ct);
        var advanceDays = await settings.GetIntAsync(AppSettingKeys.Booking.AdvanceDays, 60, ct);

        if (startUtc <= nowUtc)
        {
            throw new BusinessRuleException("The new start must be in the future.");
        }
        if (startUtc > nowUtc.AddDays(advanceDays))
        {
            throw new BusinessRuleException($"Bookings open up to {advanceDays} days in advance.");
        }
        var startIst = IstClock.ToIst(startUtc);
        var endIst = IstClock.ToIst(startUtc.AddMinutes(durationMinutes));
        if (startIst.Minute % granularity != 0 || startIst.Second != 0)
        {
            throw new BusinessRuleException($"Start time must align to {granularity}-minute slots.");
        }
        var location = workspace.Location;
        var dayCode = startIst.DayOfWeek.ToString()[..3].ToUpperInvariant();
        if (!location.WorkingDays.Split(',').Contains(dayCode))
        {
            throw new BusinessRuleException("The location is closed on the selected day.");
        }
        var open = startIst.Date + location.WorkingHoursStart.ToTimeSpan();
        var close = startIst.Date + location.WorkingHoursEnd.ToTimeSpan();
        if (startIst < open || endIst > close)
        {
            throw new BusinessRuleException("The new time falls outside working hours.");
        }
    }

    private async Task RevokeAccessTokensAsync(long bookingId, string reason, DateTime nowUtc,
        CancellationToken ct)
    {
        var tokens = await db.AccessTokens
            .Where(t => t.BookingId == bookingId && t.Status == AccessTokenStatus.Active)
            .ToListAsync(ct);
        foreach (var token in tokens)
        {
            token.Status = AccessTokenStatus.Revoked;
            token.RevokedReason = reason;
        }
        _ = nowUtc; // revocation time is carried by the audit columns
    }

    private static Payment NewPayment(Booking booking, decimal amount, PaymentMethod method,
        DateTime nowUtc) => new()
    {
        CustomerId = booking.CustomerId,
        BookingId = booking.BookingId,
        Amount = amount,
        PaymentMethod = method,
        Status = PaymentStatus.Success,
        PaidUtc = nowUtc
    };

    private static Notification BuildNotification(Booking booking, string title, string message,
        DateTime nowUtc) => new()
    {
        UserId = booking.CustomerId,
        Audience = NotificationAudience.SingleUser,
        Title = title,
        Message = message,
        Category = NotificationCategory.Booking,
        Priority = NotificationPriority.Normal,
        ReferenceType = "Booking",
        ReferenceId = booking.BookingId,
        ActionLabel = "View booking",
        ActionRoute = $"/bookings/{booking.BookingId}",
        SentUtc = nowUtc
    };
}
