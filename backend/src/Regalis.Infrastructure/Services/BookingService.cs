using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Application.Bookings;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Domain.Entities.Access;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Security;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The central booking engine (ARCHITECTURE §6–§7). Holds and confirmations run
/// serializable transactions whose conflict query takes UPDLOCK/HOLDLOCK key-range
/// locks; the filtered unique index UX_Booking_Workspace_Start is the second line of
/// defence; the retrying execution strategy replays deadlock victims (1205).
/// Design Invariant 4: a paid booking produces booking + payment + invoice + QR
/// token + notification atomically — steps succeed or fail together.
/// </summary>
public class BookingService(
    RegalisDbContext db,
    ISettingService settings,
    INumberSequenceService sequences,
    IWalletService walletService,
    IPaymentGateway paymentGateway,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IOptions<JwtOptions> jwtOptions,
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

        var strategy = db.Database.CreateExecutionStrategy();
        var booking = await strategy.ExecuteAsync(async () =>
        {
            // A retried attempt (deadlock victim) must not re-insert entities the
            // failed attempt left in the change tracker.
            db.ChangeTracker.Clear();

            await using var transaction =
                await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var conflicts = await CountConflictsWithRangeLocksAsync(
                workspace.WorkspaceId, startUtc, endUtc, excludeBookingId: null, ct);
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

    // ── The confirmed-booking transaction (ARCHITECTURE §7) ─────────────────

    public async Task<ConfirmBookingResponse> ConfirmAsync(long bookingId,
        ConfirmBookingRequest request, CancellationToken ct = default)
    {
        if (request.PaymentMethod == PaymentMethods.Cash && currentUser.UserType == UserType.Customer)
        {
            throw new ForbiddenException("Cash settlement is available at the front desk only.");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        var response = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction =
                await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var booking = await db.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Workspace)
                .Include(b => b.WorkspaceType)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct)
                ?? throw new NotFoundException("Booking", bookingId);

            if (currentUser.UserType == UserType.Customer && booking.CustomerId != currentUser.UserId)
            {
                throw new NotFoundException("Booking", bookingId);
            }
            if (booking.Status != BookingStatus.Pending)
            {
                throw new BusinessRuleException(
                    $"This booking is not awaiting payment (status {booking.Status}).");
            }
            var holdMinutes = await settings.GetIntAsync(AppSettingKeys.Booking.PendingHoldMinutes, 10, ct);
            if (booking.CreatedDate.AddMinutes(holdMinutes) <= clock.UtcNow)
            {
                throw new BusinessRuleException(
                    "The hold has expired. Start a new booking — the slot may still be free.");
            }

            // Step 1: re-verify availability under range locks, excluding our own hold.
            var conflicts = await CountConflictsWithRangeLocksAsync(
                booking.WorkspaceId, booking.StartUtc, booking.EndUtc, booking.BookingId, ct);
            if (conflicts > 0)
            {
                throw new ConflictException(
                    "The slot has been taken while the hold was pending. The hold is released.");
            }

            var result = await SettleAndFinalizeAsync(booking, request, ct);
            await transaction.CommitAsync(ct);
            return result;
        });

        logger.LogInformation("Booking {BookingNumber} confirmed: payment {PaymentNumber}, invoice {InvoiceNumber}",
            response.Booking.BookingNumber, response.PaymentNumber, response.InvoiceNumber);
        return response;
    }

    public async Task<ConfirmBookingResponse> AdminCreateAsync(AdminCreateBookingRequest request,
        CancellationToken ct = default)
    {
        if (currentUser.UserType == UserType.Customer)
        {
            throw new ForbiddenException("Admin booking is for staff users.");
        }
        var customer = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == request.CustomerId &&
                                      u.UserType == UserType.Customer &&
                                      u.Status == UserStatus.Active, ct)
            ?? throw new BusinessRuleException("The customer does not exist or is inactive.");

        var workspace = await LoadBookableWorkspaceAsync(request.WorkspaceId, ct);
        await ValidateBookingRulesAsync(workspace, request.StartUtc, request.DurationMinutes, ct);
        var price = await CalculatePriceAsync(workspace, request.DurationMinutes, ct);
        var endUtc = request.StartUtc.AddMinutes(request.DurationMinutes);

        var strategy = db.Database.CreateExecutionStrategy();
        var response = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction =
                await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var conflicts = await CountConflictsWithRangeLocksAsync(
                workspace.WorkspaceId, request.StartUtc, endUtc, excludeBookingId: null, ct);
            if (conflicts > 0)
            {
                throw new ConflictException("The selected slot is no longer available.");
            }

            var booking = new Booking
            {
                BookingNumber = await sequences.NextAsync(SequenceKeys.Booking, ct),
                CustomerId = customer.UserId,
                LocationId = workspace.LocationId,
                WorkspaceId = workspace.WorkspaceId,
                WorkspaceTypeId = workspace.WorkspaceTypeId,
                BookingDate = IstClock.IstDateOf(request.StartUtc),
                StartUtc = request.StartUtc,
                EndUtc = endUtc,
                DurationMinutes = request.DurationMinutes,
                BaseAmount = price.Base,
                TaxAmount = price.Cgst + price.Sgst,
                TotalAmount = price.Total,
                Status = BookingStatus.Pending,
                PaymentStatus = BookingPaymentStatus.Pending,
                BookingSource = BookingSource.Admin,
                CreatedByUserId = currentUser.UserId,
                Notes = request.Notes?.Trim()
            };
            db.Bookings.Add(booking);
            booking.StatusHistory.Add(new BookingStatusHistory
            {
                FromStatus = null,
                ToStatus = BookingStatus.Pending,
                ChangedByUserId = currentUser.UserId,
                ChangedUtc = clock.UtcNow,
                Reason = "Admin booking on the customer's behalf."
            });
            await db.SaveChangesAsync(ct);

            // Navigation properties needed by settlement + DTO mapping.
            booking.Customer = await db.Users.FirstAsync(u => u.UserId == customer.UserId, ct);
            booking.Workspace = await db.Workspaces.FirstAsync(w => w.WorkspaceId == workspace.WorkspaceId, ct);
            booking.WorkspaceType = await db.WorkspaceTypes.FirstAsync(t => t.WorkspaceTypeId == workspace.WorkspaceTypeId, ct);

            var result = await SettleAndFinalizeAsync(booking,
                new ConfirmBookingRequest(request.PaymentMethod, null, null, null), ct);
            await transaction.CommitAsync(ct);
            return result;
        });

        logger.LogInformation("Admin {AdminId} booked {BookingNumber} for customer {CustomerId} ({Method})",
            currentUser.UserId, response.Booking.BookingNumber, customer.UserId, request.PaymentMethod);
        return response;
    }

    /// <summary>Steps 2–7 of the confirmed-booking transaction: settle the payment,
    /// write the invoice, the access token, the notification and the history row.
    /// Runs inside the caller's serializable transaction.</summary>
    private async Task<ConfirmBookingResponse> SettleAndFinalizeAsync(Booking booking,
        ConfirmBookingRequest request, CancellationToken ct)
    {
        var nowUtc = clock.UtcNow;

        // Step 2/3: the money.
        var payment = request.PaymentMethod switch
        {
            PaymentMethods.Wallet => await SettleWithWalletAsync(booking, ct),
            PaymentMethods.Gateway => await SettleWithGatewayAsync(booking, request, ct),
            PaymentMethods.Cash => await SettleWithCashAsync(booking, ct),
            _ => throw new BusinessRuleException("Unknown payment method.")
        };
        await db.SaveChangesAsync(ct); // payment id materializes

        // Step 4: the GST invoice, snapshotted so later edits never rewrite history.
        var invoice = await BuildInvoiceAsync(booking, payment, ct);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        payment.InvoiceId = invoice.InvoiceId;

        // Step 5: the access token + signed QR payload.
        var accessToken = BuildAccessToken(booking);
        db.AccessTokens.Add(accessToken);

        // Step 6: the notification.
        db.Notifications.Add(BuildConfirmationNotification(booking, nowUtc));

        // Step 7: state + history.
        booking.Status = BookingStatus.Confirmed;
        booking.PaymentStatus = BookingPaymentStatus.Paid;
        booking.StatusHistory.Add(new BookingStatusHistory
        {
            BookingId = booking.BookingId,
            FromStatus = BookingStatus.Pending,
            ToStatus = BookingStatus.Confirmed,
            ChangedByUserId = currentUser.UserId,
            ChangedUtc = nowUtc,
            Reason = $"Payment {payment.PaymentNumber} ({request.PaymentMethod}) verified."
        });
        await db.SaveChangesAsync(ct);

        var holdMinutes = await settings.GetIntAsync(AppSettingKeys.Booking.PendingHoldMinutes, 10, ct);
        return new ConfirmBookingResponse(
            ToDto(booking, holdMinutes),
            payment.PaymentId, payment.PaymentNumber,
            invoice.InvoiceId, invoice.InvoiceNumber,
            accessToken.TokenValue, accessToken.QrPayload);
    }

    private async Task<Payment> SettleWithWalletAsync(Booking booking, CancellationToken ct)
    {
        await walletService.DebitAsync(booking.CustomerId, booking.TotalAmount,
            WalletCategory.Booking, $"Booking {booking.BookingNumber}",
            referenceType: "Booking", referenceId: booking.BookingId, ct: ct);
        var payment = new Payment
        {
            PaymentNumber = await sequences.NextAsync(SequenceKeys.Payment, ct),
            CustomerId = booking.CustomerId,
            BookingId = booking.BookingId,
            Amount = booking.TotalAmount,
            PaymentMethod = PaymentMethod.Wallet,
            Status = PaymentStatus.Success,
            PaidUtc = clock.UtcNow
        };
        db.Payments.Add(payment);
        return payment;
    }

    private async Task<Payment> SettleWithGatewayAsync(Booking booking,
        ConfirmBookingRequest request, CancellationToken ct)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p =>
            p.GatewayOrderId == request.GatewayOrderId && p.BookingId == booking.BookingId, ct)
            ?? throw new BusinessRuleException(
                "No payment order exists for this booking. Create the order first.",
                ErrorCodes.PaymentVerificationFailed);
        if (payment.Status != PaymentStatus.Pending)
        {
            throw new BusinessRuleException("This payment order is already settled.",
                ErrorCodes.PaymentVerificationFailed);
        }

        // Server-side signature verification — the client's word is never trusted.
        if (!paymentGateway.VerifySignature(request.GatewayOrderId!, request.GatewayPaymentId!,
                request.GatewaySignature!))
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = "Signature verification failed";
            await db.SaveChangesAsync(ct);
            throw new BusinessRuleException("Payment verification failed.",
                ErrorCodes.PaymentVerificationFailed);
        }

        payment.GatewayPaymentId = request.GatewayPaymentId;
        payment.GatewaySignature = request.GatewaySignature;
        payment.IsSignatureVerified = true;
        payment.Status = PaymentStatus.Success;
        payment.PaidUtc = clock.UtcNow;
        return payment;
    }

    private async Task<Payment> SettleWithCashAsync(Booking booking, CancellationToken ct)
    {
        var payment = new Payment
        {
            PaymentNumber = await sequences.NextAsync(SequenceKeys.Payment, ct),
            CustomerId = booking.CustomerId,
            BookingId = booking.BookingId,
            Amount = booking.TotalAmount,
            PaymentMethod = PaymentMethod.Cash,
            Status = PaymentStatus.Success,
            PaidUtc = clock.UtcNow
        };
        db.Payments.Add(payment);
        return payment;
    }

    private async Task<Invoice> BuildInvoiceAsync(Booking booking, Payment payment, CancellationToken ct)
    {
        var cgstPercent = await settings.GetDecimalAsync(AppSettingKeys.Tax.CgstPercent, 9m, ct);
        var sgstPercent = await settings.GetDecimalAsync(AppSettingKeys.Tax.SgstPercent, 9m, ct);
        // Split the tax the booking already carries so the invoice total equals
        // the amount actually paid, whatever the settings say today.
        var cgstAmount = Math.Round(booking.BaseAmount * cgstPercent / 100m, 2,
            MidpointRounding.AwayFromZero);
        if (cgstAmount > booking.TaxAmount)
        {
            cgstAmount = booking.TaxAmount;
        }
        var sgstAmount = booking.TaxAmount - cgstAmount;

        var invoiceDate = IstClock.IstDateOf(clock.UtcNow);
        var dueDays = await settings.GetIntAsync(AppSettingKeys.Invoice.DueDays, 7, ct);
        var startIst = IstClock.ToIst(booking.StartUtc);
        var endIst = IstClock.ToIst(booking.EndUtc);

        var invoice = new Invoice
        {
            InvoiceNumber = await sequences.NextAsync(SequenceKeys.Invoice, ct),
            CustomerId = booking.CustomerId,
            BookingId = booking.BookingId,
            LocationId = booking.LocationId,
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(dueDays),
            SubTotal = booking.BaseAmount,
            DiscountAmount = booking.DiscountAmount,
            TaxableAmount = booking.BaseAmount - booking.DiscountAmount,
            CgstPercent = cgstPercent,
            SgstPercent = sgstPercent,
            IgstPercent = 0,
            CgstAmount = cgstAmount,
            SgstAmount = sgstAmount,
            IgstAmount = 0,
            TotalAmount = booking.TotalAmount,
            PaidAmount = booking.TotalAmount,
            Status = InvoiceStatus.Paid,
            PaymentId = payment.PaymentId,
            PaidUtc = clock.UtcNow,
            CustomerName = booking.Customer.FullName,
            CustomerEmail = booking.Customer.Email,
            CustomerMobile = booking.Customer.MobileNumber,
            BillingAddress = booking.Customer.Address,
            CompanyName = await settings.GetStringAsync(AppSettingKeys.Company.Name, "Regalis WorkSpace", ct),
            CompanyGstin = await settings.GetStringAsync(AppSettingKeys.Company.Gstin, "", ct),
            CompanyAddress = await settings.GetStringAsync(AppSettingKeys.Company.Address, "", ct)
        };
        invoice.Lines.Add(new InvoiceLine
        {
            LineNumber = 1,
            Description = $"Workspace booking {booking.BookingNumber} — " +
                          $"{booking.Workspace.WorkspaceName} ({booking.Workspace.WorkspaceCode}), " +
                          $"{startIst:dd MMM yyyy HH:mm}–{endIst:HH:mm} IST",
            Quantity = Math.Round(booking.DurationMinutes / 60m, 3),
            UnitPrice = booking.Workspace.PricePerHour,
            LineAmount = booking.BaseAmount,
            TaxPercent = cgstPercent + sgstPercent,
            TaxAmount = booking.TaxAmount,
            TotalAmount = booking.TotalAmount
        });
        return invoice;
    }

    private AccessToken BuildAccessToken(Booking booking)
    {
        var tokenValue = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        return new AccessToken
        {
            TokenValue = tokenValue,
            QrPayload = SignQrPayload(tokenValue),
            UserId = booking.CustomerId,
            BookingId = booking.BookingId,
            WorkspaceId = booking.WorkspaceId,
            LocationId = booking.LocationId,
            TokenPurpose = AccessTokenPurpose.Booking,
            ValidFromUtc = booking.StartUtc,
            ValidToUtc = booking.EndUtc,
            Status = AccessTokenStatus.Active
        };
    }

    /// <summary>Compact signed QR payload: RGL1.{token}.{hmac}. The reader posts it
    /// to /access/validate, which re-verifies the signature and the token window.</summary>
    private string SignQrPayload(string tokenValue)
    {
        var signature = Convert.ToBase64String(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey),
                Encoding.UTF8.GetBytes(tokenValue)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"RGL1.{tokenValue}.{signature}";
    }

    private Notification BuildConfirmationNotification(Booking booking, DateTime nowUtc)
    {
        var startIst = IstClock.ToIst(booking.StartUtc);
        var endIst = IstClock.ToIst(booking.EndUtc);
        return new Notification
        {
            UserId = booking.CustomerId,
            Audience = NotificationAudience.SingleUser,
            Title = "Booking confirmed",
            Message = $"{booking.BookingNumber}: {booking.Workspace.WorkspaceName} on " +
                      $"{startIst:dd MMM yyyy}, {startIst:HH:mm}–{endIst:HH:mm} IST. " +
                      "Your QR access pass is ready.",
            Category = NotificationCategory.Booking,
            Priority = NotificationPriority.Normal,
            ReferenceType = "Booking",
            ReferenceId = booking.BookingId,
            ActionLabel = "View booking",
            ActionRoute = $"/bookings/{booking.BookingId}",
            SentUtc = nowUtc
        };
    }

    // ── Reads ────────────────────────────────────────────────────────────────

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

    public async Task<PagedResult<BookingDto>> GetMyBookingsAsync(string tab, PagedRequest request,
        CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var query = db.Bookings.AsNoTracking()
            .Where(b => b.CustomerId == currentUser.UserId);

        query = tab.ToLowerInvariant() switch
        {
            MyBookingTabs.Completed => query.Where(b =>
                b.Status == BookingStatus.Completed ||
                (b.EndUtc < nowUtc &&
                 (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn))),
            MyBookingTabs.Cancelled => query.Where(b =>
                b.Status == BookingStatus.Cancelled || b.Status == BookingStatus.Expired ||
                b.Status == BookingStatus.NoShow),
            _ => query.Where(b =>
                b.EndUtc >= nowUtc &&
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed ||
                 b.Status == BookingStatus.CheckedIn))
        };
        query = tab.Equals(MyBookingTabs.Upcoming, StringComparison.OrdinalIgnoreCase)
            ? query.OrderBy(b => b.StartUtc)
            : query.OrderByDescending(b => b.StartUtc);

        return await PageAsync(query, request, ct);
    }

    public async Task<PagedResult<BookingDto>> GetBookingsAsync(BookingListRequest request,
        CancellationToken ct = default)
    {
        var query = db.Bookings.AsNoTracking();
        if (request.From is not null)
        {
            query = query.Where(b => b.BookingDate >= request.From);
        }
        if (request.To is not null)
        {
            query = query.Where(b => b.BookingDate <= request.To);
        }
        if (request.Status is not null)
        {
            query = query.Where(b => (byte)b.Status == request.Status);
        }
        if (request.LocationId is not null)
        {
            query = query.Where(b => b.LocationId == request.LocationId);
        }
        if (request.WorkspaceTypeId is not null)
        {
            query = query.Where(b => b.WorkspaceTypeId == request.WorkspaceTypeId);
        }
        if (request.CustomerId is not null)
        {
            query = query.Where(b => b.CustomerId == request.CustomerId);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(b =>
                b.BookingNumber.Contains(term) ||
                b.Customer.FullName.Contains(term) ||
                b.Customer.Email.Contains(term));
        }
        query = query.OrderByDescending(b => b.StartUtc);
        return await PageAsync(query, request, ct);
    }

    private async Task<PagedResult<BookingDto>> PageAsync(IQueryable<Booking> query,
        PagedRequest request, CancellationToken ct)
    {
        var holdMinutes = await settings.GetIntAsync(AppSettingKeys.Booking.PendingHoldMinutes, 10, ct);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(b => b.Customer)
            .Include(b => b.Workspace)
            .Include(b => b.WorkspaceType)
            .ToListAsync(ct);
        return PagedResult<BookingDto>.Create(
            items.Select(b => ToDto(b, holdMinutes)).ToList(),
            request.Page, request.PageSize, totalCount);
    }

    // ── The locking conflict check (DATABASE_DESIGN §5) ─────────────────────

    /// <summary>Runs the documented availability predicate under UPDLOCK/HOLDLOCK
    /// inside the caller's serializable transaction — the range lock blocks a
    /// concurrent insert of a conflicting row until we commit. On confirmation the
    /// booking's own Pending row is excluded.</summary>
    private Task<int> CountConflictsWithRangeLocksAsync(int workspaceId,
        DateTime startUtc, DateTime endUtc, long? excludeBookingId, CancellationToken ct) =>
        BookingEngineLocks.CountConflictsAsync(db, workspaceId, startUtc, endUtc, excludeBookingId, ct);

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
