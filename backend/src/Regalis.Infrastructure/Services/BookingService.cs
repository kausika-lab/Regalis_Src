using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Bookings;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The central booking engine (ARCHITECTURE §6). Two customers can hit "book" on the
/// same slot in the same millisecond — frontend checks are advisory only. The hold
/// runs a serializable transaction whose conflict query takes UPDLOCK/HOLDLOCK
/// key-range locks, blocking a second transaction from inserting a conflicting row.
/// The filtered unique index UX_Booking_Workspace_Start is the second line of
/// defence; the retrying execution strategy replays deadlock victims (1205).
/// </summary>
public class BookingService(
    RegalisDbContext db,
    ISettingService settings,
    INumberSequenceService sequences,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<BookingService> logger) : IBookingService
{
    public async Task<QuoteResponse> QuoteAsync(QuoteRequest request, CancellationToken ct = default)
    {
        var workspace = await LoadBookableWorkspaceAsync(request.WorkspaceId, ct);
        await ValidateBookingRulesAsync(workspace, request.StartUtc, request.DurationMinutes, ct);

        var endUtc = request.StartUtc.AddMinutes(request.DurationMinutes);
        var price = await CalculatePriceAsync(workspace, request.DurationMinutes, ct);

        return new QuoteResponse(
            workspace.WorkspaceId, workspace.WorkspaceName, request.StartUtc, endUtc,
            request.DurationMinutes, price.Base, price.CgstPercent, price.Cgst,
            price.SgstPercent, price.Sgst, price.Total);
    }

    public async Task<BookingDto> CreateHoldAsync(CreateBookingRequest request,
        CancellationToken ct = default)
    {
        if (currentUser.UserType != UserType.Customer)
        {
            // Admins book on a customer's behalf through POST /bookings/admin.
            throw new ForbiddenException("Only customers can create bookings here.");
        }

        var workspace = await LoadBookableWorkspaceAsync(request.WorkspaceId, ct);
        await ValidateBookingRulesAsync(workspace, request.StartUtc, request.DurationMinutes, ct);

        var startUtc = request.StartUtc;
        var endUtc = startUtc.AddMinutes(request.DurationMinutes);
        var price = await CalculatePriceAsync(workspace, request.DurationMinutes, ct);
        var holdMinutes = await settings.GetIntAsync(AppSettingKeys.Booking.PendingHoldMinutes, 10, ct);

        // The retrying execution strategy replays the whole unit on transient
        // failures (deadlock victim 1205 included) with jittered backoff.
        var strategy = db.Database.CreateExecutionStrategy();
        var booking = await strategy.ExecuteAsync(async () =>
        {
            // A retried attempt (deadlock victim) must not re-insert entities the
            // failed attempt left in the change tracker.
            db.ChangeTracker.Clear();

            await using var transaction =
                await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var conflicts = await CountConflictsWithRangeLocksAsync(
                workspace.WorkspaceId, startUtc, endUtc, ct);
            if (conflicts > 0)
            {
                // Losing the race is the documented 409 SLOT_UNAVAILABLE outcome.
                throw new ConflictException(
                    "The selected slot is no longer available. Pick another time or workspace.");
            }

            var nowUtc = clock.UtcNow;
            var entity = new Booking
            {
                BookingNumber = await sequences.NextAsync(SequenceKeys.Booking, ct),
                CustomerId = currentUser.UserId,
                LocationId = workspace.LocationId,
                WorkspaceId = workspace.WorkspaceId,
                WorkspaceTypeId = workspace.WorkspaceTypeId,
                BookingDate = IstClock.IstDateOf(startUtc),
                StartUtc = startUtc,
                EndUtc = endUtc,
                DurationMinutes = request.DurationMinutes,
                BaseAmount = price.Base,
                TaxAmount = price.Cgst + price.Sgst,
                TotalAmount = price.Total,
                Status = BookingStatus.Pending,
                PaymentStatus = BookingPaymentStatus.Pending,
                BookingSource = BookingSource.Customer,
                CreatedByUserId = currentUser.UserId,
                Notes = request.Notes?.Trim()
            };
            db.Bookings.Add(entity);
            entity.StatusHistory.Add(new BookingStatusHistory
            {
                FromStatus = null,
                ToStatus = BookingStatus.Pending,
                ChangedByUserId = currentUser.UserId,
                ChangedUtc = nowUtc,
                Reason = $"Hold created; expires in {holdMinutes} minutes unless payment completes."
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return entity;
        });

        logger.LogInformation(
            "Booking hold {BookingNumber} created for customer {CustomerId}: workspace {WorkspaceId} {Start}–{End}",
            booking.BookingNumber, booking.CustomerId, booking.WorkspaceId, startUtc, endUtc);

        return await GetBookingAsync(booking.BookingId, ct);
    }

    public async Task<BookingDto> GetBookingAsync(long bookingId, CancellationToken ct = default)
    {
        var booking = await db.Bookings.AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.Workspace)
            .Include(b => b.WorkspaceType)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct)
            ?? throw new NotFoundException("Booking", bookingId);

        // Row-level scoping (ROLE_PERMISSION_MATRIX §6): a customer can never read
        // another customer's booking, whatever id they pass.
        if (currentUser.UserType == UserType.Customer && booking.CustomerId != currentUser.UserId)
        {
            throw new NotFoundException("Booking", bookingId);
        }

        var holdMinutes = await settings.GetIntAsync(AppSettingKeys.Booking.PendingHoldMinutes, 10, ct);
        return ToDto(booking, holdMinutes);
    }

    // ── The locking conflict check (DATABASE_DESIGN §5) ─────────────────────

    /// <summary>Runs the documented availability predicate under UPDLOCK/HOLDLOCK
    /// inside the caller's serializable transaction — the range lock blocks a
    /// concurrent insert of a conflicting row until we commit.</summary>
    private async Task<int> CountConflictsWithRangeLocksAsync(int workspaceId,
        DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = """
            SELECT
              (SELECT COUNT(*) FROM dbo.TRN_Booking WITH (UPDLOCK, HOLDLOCK)
                WHERE WorkspaceId = @workspaceId AND IsDeleted = 0
                  AND Status IN (1,2,3)
                  AND StartUtc < @endUtc AND @startUtc < EndUtc)
            + (SELECT COUNT(*) FROM dbo.TRN_WorkspaceBlock WITH (UPDLOCK, HOLDLOCK)
                WHERE WorkspaceId = @workspaceId AND IsDeleted = 0 AND IsActive = 1
                  AND StartUtc < @endUtc AND @startUtc < EndUtc);
            """;
        AddParameter(command, "@workspaceId", workspaceId);
        AddParameter(command, "@startUtc", startUtc);
        AddParameter(command, "@endUtc", endUtc);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    // ── Business rules (all configurable — Invariant 6) ─────────────────────

    private async Task<Workspace> LoadBookableWorkspaceAsync(int workspaceId, CancellationToken ct)
    {
        var workspace = await db.Workspaces.AsNoTracking()
            .Include(w => w.Location)
            .FirstOrDefaultAsync(w => w.WorkspaceId == workspaceId, ct)
            ?? throw new NotFoundException("Workspace", workspaceId);
        if (!workspace.IsBookable || workspace.Status != WorkspaceStatus.Available || !workspace.IsActive)
        {
            throw new BusinessRuleException("This workspace is not open for booking.");
        }
        return workspace;
    }

    private async Task ValidateBookingRulesAsync(Workspace workspace, DateTime startUtc,
        int durationMinutes, CancellationToken ct)
    {
        var nowUtc = clock.UtcNow;
        var granularity = await settings.GetIntAsync(AppSettingKeys.Booking.SlotGranularityMinutes, 30, ct);
        var minMinutes = await settings.GetIntAsync(AppSettingKeys.Booking.MinMinutes, 30, ct);
        var maxMinutes = await settings.GetIntAsync(AppSettingKeys.Booking.MaxMinutes, 480, ct);
        var advanceDays = await settings.GetIntAsync(AppSettingKeys.Booking.AdvanceDays, 60, ct);

        if (startUtc <= nowUtc)
        {
            throw new BusinessRuleException("The booking must start in the future.");
        }
        if (startUtc > nowUtc.AddDays(advanceDays))
        {
            throw new BusinessRuleException($"Bookings open up to {advanceDays} days in advance.");
        }
        if (durationMinutes < minMinutes || durationMinutes > maxMinutes)
        {
            throw new BusinessRuleException(
                $"Duration must be between {minMinutes} and {maxMinutes} minutes.");
        }
        if (durationMinutes % granularity != 0)
        {
            throw new BusinessRuleException($"Duration must be a multiple of {granularity} minutes.");
        }

        var startIst = IstClock.ToIst(startUtc);
        var endIst = IstClock.ToIst(startUtc.AddMinutes(durationMinutes));
        if (startIst.Minute % granularity != 0 || startIst.Second != 0)
        {
            throw new BusinessRuleException($"Start time must align to {granularity}-minute slots.");
        }

        // The stay must fit inside the location's working window of one IST day.
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
            throw new BusinessRuleException(
                $"Bookings must fall within working hours " +
                $"({location.WorkingHoursStart:HH\\:mm}–{location.WorkingHoursEnd:HH\\:mm} IST).");
        }
    }

    private sealed record Price(decimal Base, decimal CgstPercent, decimal Cgst,
        decimal SgstPercent, decimal Sgst, decimal Total);

    private async Task<Price> CalculatePriceAsync(Workspace workspace, int durationMinutes,
        CancellationToken ct)
    {
        var cgstPercent = await settings.GetDecimalAsync(AppSettingKeys.Tax.CgstPercent, 9m, ct);
        var sgstPercent = await settings.GetDecimalAsync(AppSettingKeys.Tax.SgstPercent, 9m, ct);

        var baseAmount = Math.Round(workspace.PricePerHour * durationMinutes / 60m, 2,
            MidpointRounding.AwayFromZero);
        var cgst = Math.Round(baseAmount * cgstPercent / 100m, 2, MidpointRounding.AwayFromZero);
        var sgst = Math.Round(baseAmount * sgstPercent / 100m, 2, MidpointRounding.AwayFromZero);
        return new Price(baseAmount, cgstPercent, cgst, sgstPercent, sgst, baseAmount + cgst + sgst);
    }

    private static BookingDto ToDto(Booking b, int holdMinutes) => new(
        b.BookingId, b.BookingNumber, b.CustomerId, b.Customer.FullName, b.LocationId,
        b.WorkspaceId, b.Workspace.WorkspaceCode, b.Workspace.WorkspaceName,
        b.WorkspaceType.TypeName, b.StartUtc, b.EndUtc, b.DurationMinutes,
        b.BaseAmount, b.TaxAmount, b.TotalAmount,
        (byte)b.Status, (byte)b.PaymentStatus, (byte)b.BookingSource, b.Notes, b.CreatedDate,
        b.Status == BookingStatus.Pending ? b.CreatedDate.AddMinutes(holdMinutes) : null);
}
