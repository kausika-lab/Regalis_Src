using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Who may confirm a booking with "Pay later at the front desk". Every limit lives in
/// MST_AppSetting (PayLater.*); 0 means no limit. Decisions of 27 Oct 2026: hourly,
/// multi-day and monthly bookings; no amount limit; two unpaid pay-later bookings per
/// customer at once.
/// </summary>
public static class PayLaterPolicy
{
    public const string Enabled = "PayLater.Enabled";
    public const string AllowedBookingKinds = "PayLater.AllowedBookingKinds";
    public const string MaxAmount = "PayLater.MaxAmount";
    public const string MaxOutstandingPerCustomer = "PayLater.MaxOutstandingPerCustomer";
    public const string MaxOpenBookings = "PayLater.MaxOpenBookings";
    public const string DueBeforeStartMinutes = "PayLater.DueBeforeStartMinutes";
    public const string AutoCancelAfterStartMinutes = "PayLater.AutoCancelAfterStartMinutes";
    public const string BlockCheckInUntilPaid = "PayLater.BlockCheckInUntilPaid";
    public const string ReminderHoursBefore = "PayLater.ReminderHoursBefore";
    public const string AutoBlockAfterUnpaidNoShows = "PayLater.AutoBlockAfterUnpaidNoShows";

    /// <summary>The reason an unpaid pay-later booking was released; also how the
    /// automatic block counts a customer's unpaid no-shows.</summary>
    public const string UnpaidReleaseReason = "Pay later: not paid by the start time; slot released.";

    /// <summary>Hourly, MultiDay or Monthly, as PayLater.AllowedBookingKinds names them.</summary>
    public static string KindOf(Booking booking)
    {
        if (booking.BillingMonths.HasValue)
        {
            return "Monthly";
        }
        return IstClock.IstDateOf(booking.StartUtc) == IstClock.IstDateOf(booking.EndUtc.AddTicks(-1)) ? "Hourly" : "MultiDay";
    }

    /// <summary>Null when the booking may be paid later, otherwise the reason it may not,
    /// in words the customer sees.</summary>
    public static async Task<string?> CheckAsync(RegalisDbContext db, ISettingService settings, Booking booking, CancellationToken ct)
    {
        if (!await settings.GetBoolAsync(Enabled, false, ct))
        {
            return "Pay later is not available at the moment.";
        }

        string kinds = await settings.GetStringAsync(AllowedBookingKinds, "Hourly,MultiDay,Monthly", ct);
        string kind = KindOf(booking);
        if (!kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(kind, StringComparer.OrdinalIgnoreCase))
        {
            return kind switch
            {
                "Monthly" => "Monthly bookings are paid in advance.",
                "MultiDay" => "Bookings over several days are paid in advance.",
                _ => "This booking is paid in advance."
            };
        }

        bool allowedForCustomer = await db.Users.AsNoTracking()
            .Where(u => u.UserId == booking.CustomerId)
            .Select(u => u.PayLaterAllowed)
            .FirstOrDefaultAsync(ct);
        if (!allowedForCustomer)
        {
            return "Pay later is not available on your account. Please pay now, or speak to the front desk.";
        }

        decimal maxAmount = await settings.GetDecimalAsync(MaxAmount, 0m, ct);
        if (maxAmount > 0m && booking.TotalAmount > maxAmount)
        {
            return $"Pay later is available up to ₹{maxAmount:N0} a booking.";
        }

        var open = await db.Bookings.AsNoTracking()
            .Where(b => b.CustomerId == booking.CustomerId
                        && b.BookingId != booking.BookingId
                        && b.IsPayLater
                        && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn)
                        && b.AmountPaid < b.TotalAmount)
            .Select(b => new { b.WorkspaceId, b.CreatedDate, Owed = b.TotalAmount - b.AmountPaid })
            .ToListAsync(ct);
        decimal owed = open.Sum(b => b.Owed);

        // A multi-day checkout is one booking per day, all held within the hold window on
        // the same workspace: count each checkout once, and never count the checkout this
        // booking belongs to.
        int window = await settings.GetIntAsync("Booking.PendingHoldMinutes", 10, ct);
        bool SameCheckout(int workspaceId, DateTime created, int otherWorkspaceId, DateTime otherCreated) =>
            workspaceId == otherWorkspaceId && Math.Abs((created - otherCreated).TotalMinutes) <= window;
        int openCount = 0;
        int? lastWorkspace = null;
        DateTime lastCreated = DateTime.MinValue;
        foreach (var b in open.OrderBy(b => b.WorkspaceId).ThenBy(b => b.CreatedDate))
        {
            if (booking.BookingId > 0 && SameCheckout(booking.WorkspaceId, booking.CreatedDate, b.WorkspaceId, b.CreatedDate))
            {
                continue;
            }
            if (lastWorkspace == null || !SameCheckout(lastWorkspace.Value, lastCreated, b.WorkspaceId, b.CreatedDate))
            {
                openCount++;
            }
            lastWorkspace = b.WorkspaceId;
            lastCreated = b.CreatedDate;
        }

        // Unpaid pay-later passes and memberships share the same limits.
        var (passCount, passOwed) = await OpenPassesAsync(db, booking.CustomerId, null, ct);
        return await LimitsAsync(settings, openCount + passCount, owed + passOwed, booking.TotalAmount, ct);
    }

    /// <summary>The open-items and amount-owed limits, shared by bookings and passes.</summary>
    private static async Task<string?> LimitsAsync(ISettingService settings, int openCount, decimal owed, decimal adding, CancellationToken ct)
    {
        int maxOpen = await settings.GetIntAsync(MaxOpenBookings, 2, ct);
        if (maxOpen > 0 && openCount >= maxOpen)
        {
            return $"You already have {openCount} unpaid pay-later purchase{(openCount == 1 ? "" : "s")}. Pay for {(openCount == 1 ? "it" : "them")} at the front desk first, or pay now.";
        }

        decimal maxOwed = await settings.GetDecimalAsync(MaxOutstandingPerCustomer, 0m, ct);
        if (maxOwed > 0m && owed + adding > maxOwed)
        {
            return $"Pay later is available while you owe up to ₹{maxOwed:N0} in total.";
        }

        return null;
    }

    // ── Day passes and memberships (23-28 Oct 2026) ─────────────────────────

    /// <summary>"DayPass,Membership" by default; a membership is a monthly (MEM-) plan.</summary>
    public const string AllowedPassKinds = "PayLater.AllowedPassKinds";

    public static string PassKindOf(Domain.Entities.Masters.PassPlan plan) =>
        plan.PassType == PassType.DayPass ? "DayPass" : "Membership";

    /// <summary>Unpaid pay-later passes the customer holds (active, not past their last day).</summary>
    private static async Task<(int Count, decimal Owed)> OpenPassesAsync(RegalisDbContext db, long customerId, long? exceptPassId, CancellationToken ct)
    {
        var owedList = await db.CustomerPasses.AsNoTracking()
            .Where(p => p.CustomerId == customerId && p.IsPayLater && p.Status == CustomerPassStatus.Active
                        && p.AmountPaid < p.TotalAmount && (exceptPassId == null || p.CustomerPassId != exceptPassId))
            .Select(p => p.TotalAmount - p.AmountPaid)
            .ToListAsync(ct);
        return (owedList.Count, owedList.Sum());
    }

    /// <summary>Unpaid pay-later bookings, one per checkout, and what they owe.</summary>
    private static async Task<(int Count, decimal Owed)> OpenBookingsAsync(RegalisDbContext db, ISettingService settings, long customerId, CancellationToken ct)
    {
        var open = await db.Bookings.AsNoTracking()
            .Where(b => b.CustomerId == customerId && b.IsPayLater
                        && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn)
                        && b.AmountPaid < b.TotalAmount)
            .Select(b => new { b.WorkspaceId, b.CreatedDate, Owed = b.TotalAmount - b.AmountPaid })
            .ToListAsync(ct);
        int window = await settings.GetIntAsync("Booking.PendingHoldMinutes", 10, ct);
        int count = 0;
        int? lastWorkspace = null;
        DateTime lastCreated = DateTime.MinValue;
        foreach (var b in open.OrderBy(b => b.WorkspaceId).ThenBy(b => b.CreatedDate))
        {
            if (lastWorkspace == null || lastWorkspace != b.WorkspaceId || Math.Abs((b.CreatedDate - lastCreated).TotalMinutes) > window)
            {
                count++;
            }
            lastWorkspace = b.WorkspaceId;
            lastCreated = b.CreatedDate;
        }
        return (count, open.Sum(b => b.Owed));
    }

    /// <summary>Null when this pass may be bought with pay later, otherwise why not.</summary>
    public static async Task<string?> CheckPassAsync(RegalisDbContext db, ISettingService settings, long customerId,
        Domain.Entities.Masters.PassPlan plan, decimal total, CancellationToken ct)
    {
        if (!await settings.GetBoolAsync(Enabled, false, ct))
        {
            return "Pay later is not available at the moment.";
        }
        string kinds = await settings.GetStringAsync(AllowedPassKinds, "DayPass,Membership", ct);
        if (!kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(PassKindOf(plan), StringComparer.OrdinalIgnoreCase))
        {
            return plan.PassType == PassType.DayPass ? "Day passes are paid in advance." : "Memberships are paid in advance.";
        }
        bool allowedForCustomer = await db.Users.AsNoTracking()
            .Where(u => u.UserId == customerId).Select(u => u.PayLaterAllowed).FirstOrDefaultAsync(ct);
        if (!allowedForCustomer)
        {
            return "Pay later is not available on your account. Please pay now, or speak to the front desk.";
        }
        decimal maxAmount = await settings.GetDecimalAsync(MaxAmount, 0m, ct);
        if (maxAmount > 0m && total > maxAmount)
        {
            return $"Pay later is available up to ₹{maxAmount:N0} a purchase.";
        }
        var (bookingCount, bookingOwed) = await OpenBookingsAsync(db, settings, customerId, ct);
        var (passCount, passOwed) = await OpenPassesAsync(db, customerId, null, ct);
        return await LimitsAsync(settings, bookingCount + passCount, bookingOwed + passOwed, total, ct);
    }

    /// <summary>Before choosing a plan: may this customer pay later for a pass, and for which kinds.</summary>
    public static async Task<(string? Reason, string[] Kinds)> PassStatusAsync(RegalisDbContext db, ISettingService settings, long customerId, CancellationToken ct)
    {
        if (!await settings.GetBoolAsync(Enabled, false, ct))
        {
            return ("Pay later is not available at the moment.", Array.Empty<string>());
        }
        string[] kinds = (await settings.GetStringAsync(AllowedPassKinds, "DayPass,Membership", ct))
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool allowedForCustomer = await db.Users.AsNoTracking()
            .Where(u => u.UserId == customerId).Select(u => u.PayLaterAllowed).FirstOrDefaultAsync(ct);
        if (!allowedForCustomer)
        {
            return ("Pay later is not available on your account. Please pay now, or speak to the front desk.", Array.Empty<string>());
        }
        var (bookingCount, bookingOwed) = await OpenBookingsAsync(db, settings, customerId, ct);
        var (passCount, passOwed) = await OpenPassesAsync(db, customerId, null, ct);
        string? reason = await LimitsAsync(settings, bookingCount + passCount, bookingOwed + passOwed, 0m, ct);
        return (reason, reason == null ? kinds : Array.Empty<string>());
    }

    /// <summary>A pay-later pass is due at the start of its first day (IST).</summary>
    public static DateTime PassDueUtc(DateOnly validFrom) =>
        IstClock.ToUtc(validFrom.ToDateTime(TimeOnly.MinValue));

    /// <summary>Before any hold exists: may this customer pay later at all, and for which
    /// booking kinds. Checks the switch, the customer's flag and the open-bookings limit;
    /// the amount limits are checked per booking at confirmation.</summary>
    public static async Task<(string? Reason, string[] Kinds)> CustomerStatusAsync(RegalisDbContext db, ISettingService settings, long customerId, CancellationToken ct)
    {
        string[] kinds = (await settings.GetStringAsync(AllowedBookingKinds, "Hourly,MultiDay,Monthly", ct))
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!await settings.GetBoolAsync(Enabled, false, ct))
        {
            return ("Pay later is not available at the moment.", Array.Empty<string>());
        }
        // A zero-amount probe that matches no real booking runs the per-customer checks only.
        var probe = new Booking { BookingId = -1, CustomerId = customerId, TotalAmount = 0m, BillingMonths = 1 };
        string? reason = await CheckAsync(db, settings, probe, ct);
        if (reason != null && reason.StartsWith("Monthly", StringComparison.Ordinal))
        {
            reason = null;
        }
        return (reason, reason == null ? kinds : Array.Empty<string>());
    }

    /// <summary>When the money is due: the start, less PayLater.DueBeforeStartMinutes.</summary>
    public static async Task<DateTime> DueUtcAsync(ISettingService settings, Booking booking, CancellationToken ct)
    {
        int before = await settings.GetIntAsync(DueBeforeStartMinutes, 0, ct);
        return booking.StartUtc.AddMinutes(-Math.Max(0, before));
    }
}
