using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Application.Bookings;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Domain.Entities.Access;
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
/// Day / Monthly passes (M15). A purchase follows the same atomic pattern as a
/// booking confirmation: verified payment, TRN_CustomerPass, GST invoice with
/// snapshots, an access token valid for the whole pass window, a notification.
/// </summary>
public class PassService(
    RegalisDbContext db,
    ISettingService settings,
    INumberSequenceService sequences,
    IWalletService walletService,
    IPaymentGateway paymentGateway,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IOptions<JwtOptions> jwtOptions,
    ILogger<PassService> logger) : IPassService
{
    // ── Plans ────────────────────────────────────────────────────────────────

    public async Task<List<PassPlanDto>> GetActivePlansAsync(byte? passType,
        CancellationToken ct = default)
    {
        var query = db.PassPlans.AsNoTracking().Where(p => p.IsActive);
        if (passType is not null)
        {
            query = query.Where(p => (byte)p.PassType == passType);
        }
        var plans = await query.OrderBy(p => p.DisplayOrder)
            .Include(p => p.Location).ToListAsync(ct);
        return await MapPlansAsync(plans, ct);
    }

    public async Task<List<PassPlanDto>> GetAllPlansAsync(CancellationToken ct = default)
    {
        var plans = await db.PassPlans.AsNoTracking()
            .Include(p => p.Location)
            .OrderBy(p => p.PassType).ThenBy(p => p.DisplayOrder)
            .ToListAsync(ct);
        return await MapPlansAsync(plans, ct);
    }

    public async Task<PassPlanDto> CreatePlanAsync(SavePassPlanRequest request,
        CancellationToken ct = default)
    {
        var code = request.PlanCode.Trim().ToUpperInvariant();
        if (await db.PassPlans.AnyAsync(p => p.PlanCode == code, ct))
        {
            throw new BusinessRuleException("A plan with this code already exists.",
                ErrorCodes.DuplicateRecord);
        }
        var plan = new PassPlan { PlanCode = code };
        Apply(plan, request);
        db.PassPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return (await MapPlansAsync([plan], ct))[0];
    }

    public async Task<PassPlanDto> UpdatePlanAsync(int passPlanId, SavePassPlanRequest request,
        CancellationToken ct = default)
    {
        var plan = await db.PassPlans.Include(p => p.Location)
            .FirstOrDefaultAsync(p => p.PassPlanId == passPlanId, ct)
            ?? throw new NotFoundException("PassPlan", passPlanId);
        Apply(plan, request);
        await db.SaveChangesAsync(ct);
        return (await MapPlansAsync([plan], ct))[0];
    }

    public async Task DeletePlanAsync(int passPlanId, CancellationToken ct = default)
    {
        var plan = await db.PassPlans.FirstOrDefaultAsync(p => p.PassPlanId == passPlanId, ct)
                   ?? throw new NotFoundException("PassPlan", passPlanId);
        if (await db.CustomerPasses.AnyAsync(
                p => p.PassPlanId == passPlanId && p.Status == CustomerPassStatus.Active, ct))
        {
            throw new BusinessRuleException(
                "Active customer passes use this plan. Deactivate it instead.");
        }
        db.PassPlans.Remove(plan); // interceptor converts to soft delete
        await db.SaveChangesAsync(ct);
    }

    /// <summary>A membership (plan code MEM-…, the plans the Membership screen sells) is
    /// numbered MEM-000006; any other pass keeps PASS-000006. Both take the next value of
    /// the one CustomerPass counter, so the two series can never produce the same number.</summary>
    internal static string PassNumberFor(string planCode, string sequenceNumber) =>
        planCode.StartsWith("MEM-", StringComparison.OrdinalIgnoreCase)
        && sequenceNumber.StartsWith("PASS-", StringComparison.Ordinal)
            ? "MEM-" + sequenceNumber["PASS-".Length..]
            : sequenceNumber;

    private static void Apply(PassPlan plan, SavePassPlanRequest request)
    {
        plan.PlanName = request.PlanName.Trim();
        plan.PassType = (PassType)request.PassType;
        plan.LocationId = request.LocationId;
        plan.Price = request.Price;
        plan.ValidityDays = request.ValidityDays;
        plan.MaxGuests = request.MaxGuests;
        plan.Description = request.Description?.Trim();
        plan.TermsAndConditions = request.TermsAndConditions?.Trim();
        plan.DisplayOrder = request.DisplayOrder;
        plan.IsActive = request.IsActive;
    }

    private async Task<List<PassPlanDto>> MapPlansAsync(IEnumerable<PassPlan> plans,
        CancellationToken ct)
    {
        var (cgstPercent, sgstPercent) = await TaxPercentsAsync(ct);
        return plans.Select(p =>
        {
            var (cgst, sgst, total) = PriceWithGst(p.Price, cgstPercent, sgstPercent);
            return new PassPlanDto(p.PassPlanId, p.PlanCode, p.PlanName, (byte)p.PassType,
                p.LocationId, p.Location?.LocationName, p.Price, p.ValidityDays, p.MaxGuests,
                p.Description, p.TermsAndConditions, p.DisplayOrder, p.IsActive, cgst, sgst, total);
        }).ToList();
    }

    // ── Purchase ─────────────────────────────────────────────────────────────

    public async Task<PaymentOrderResponse> CreatePurchaseOrderAsync(PassOrderRequest request,
        CancellationToken ct = default)
    {
        var plan = await RequireActivePlanAsync(request.PassPlanId, ct);
        var (cgstPercent, sgstPercent) = await TaxPercentsAsync(ct);
        var (_, _, total) = PriceWithGst(plan.Price, cgstPercent, sgstPercent);

        var order = await paymentGateway.CreateOrderAsync(total, "INR", $"PASS-{plan.PlanCode}", ct);
        var payment = new Payment
        {
            PaymentNumber = await sequences.NextAsync(SequenceKeys.Payment, ct),
            CustomerId = currentUser.UserId,
            Amount = total,
            PaymentMethod = PaymentMethod.Upi,
            Gateway = paymentGateway.ProviderName,
            GatewayOrderId = order.OrderId,
            Status = PaymentStatus.Pending
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);
        return new PaymentOrderResponse(payment.PaymentId, payment.PaymentNumber,
            order.OrderId, order.KeyId, total, "INR", paymentGateway.ProviderName);
    }

    public async Task<PurchasePassResponse> PurchaseAsync(PurchasePassRequest request,
        CancellationToken ct = default)
    {
        if (currentUser.UserType != UserType.Customer && request.PaymentMethod != PaymentMethods.Cash)
        {
            throw new ForbiddenException("Staff sell passes for cash at the front desk.");
        }
        if (request.PaymentMethod == PaymentMethods.Cash && currentUser.UserType == UserType.Customer)
        {
            throw new ForbiddenException("Cash settlement is available at the front desk only.");
        }

        var plan = await RequireActivePlanAsync(request.PassPlanId, ct);
        var todayIst = IstClock.IstDateOf(clock.UtcNow);
        if (request.ValidFrom < todayIst)
        {
            throw new BusinessRuleException("The pass cannot start in the past.");
        }
        var validTo = request.ValidFrom.AddDays(plan.ValidityDays - 1);
        var (cgstPercent, sgstPercent) = await TaxPercentsAsync(ct);
        var (cgst, sgst, total) = PriceWithGst(plan.Price, cgstPercent, sgstPercent);

        var customerId = currentUser.UserId;
        var customer = await db.Users.AsNoTracking()
            .FirstAsync(u => u.UserId == customerId, ct);

        var strategy = db.Database.CreateExecutionStrategy();
        var response = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var nowUtc = clock.UtcNow;

            // Settle the money first — same trust rules as bookings.
            Payment payment;
            switch (request.PaymentMethod)
            {
                case PaymentMethods.Wallet:
                    await walletService.DebitAsync(customerId, total,
                        plan.PassType == PassType.DayPass ? WalletCategory.DayPass : WalletCategory.MonthlyPass,
                        $"{plan.PlanName} purchase", ct: ct);
                    payment = new Payment
                    {
                        PaymentNumber = await sequences.NextAsync(SequenceKeys.Payment, ct),
                        CustomerId = customerId,
                        Amount = total,
                        PaymentMethod = PaymentMethod.Wallet,
                        Status = PaymentStatus.Success,
                        PaidUtc = nowUtc
                    };
                    db.Payments.Add(payment);
                    break;
                case PaymentMethods.Cash:
                    payment = new Payment
                    {
                        PaymentNumber = await sequences.NextAsync(SequenceKeys.Payment, ct),
                        CustomerId = customerId,
                        Amount = total,
                        PaymentMethod = PaymentMethod.Cash,
                        Status = PaymentStatus.Success,
                        PaidUtc = nowUtc
                    };
                    db.Payments.Add(payment);
                    break;
                default:
                    payment = await db.Payments.FirstOrDefaultAsync(p =>
                        p.GatewayOrderId == request.GatewayOrderId && p.CustomerId == customerId, ct)
                        ?? throw new BusinessRuleException("No payment order exists for this purchase.",
                            ErrorCodes.PaymentVerificationFailed);
                    if (payment.Status != PaymentStatus.Pending)
                    {
                        throw new BusinessRuleException("This payment order is already settled.",
                            ErrorCodes.PaymentVerificationFailed);
                    }
                    if (!paymentGateway.VerifySignature(request.GatewayOrderId!,
                            request.GatewayPaymentId!, request.GatewaySignature!))
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
                    payment.PaidUtc = nowUtc;
                    break;
            }
            await db.SaveChangesAsync(ct);

            var locationId = plan.LocationId
                ?? await db.Locations.Where(l => l.IsDefault).Select(l => l.LocationId).FirstAsync(ct);
            var pass = new CustomerPass
            {
                PassNumber = PassNumberFor(plan.PlanCode,
                    await sequences.NextAsync(SequenceKeys.CustomerPass, ct)),
                CustomerId = customerId,
                PassPlanId = plan.PassPlanId,
                LocationId = locationId,
                PassType = plan.PassType,
                PurchaseUtc = nowUtc,
                ValidFrom = request.ValidFrom,
                ValidTo = validTo,
                GuestCount = 0,
                Price = plan.Price,
                TaxAmount = cgst + sgst,
                TotalAmount = total,
                Status = CustomerPassStatus.Active,
                PaymentId = payment.PaymentId
            };
            db.CustomerPasses.Add(pass);
            await db.SaveChangesAsync(ct);
            payment.CustomerPassId = pass.CustomerPassId;

            var invoiceDate = IstClock.IstDateOf(nowUtc);
            var invoice = new Invoice
            {
                InvoiceNumber = await sequences.NextAsync(SequenceKeys.Invoice, ct),
                CustomerId = customerId,
                CustomerPassId = pass.CustomerPassId,
                LocationId = locationId,
                InvoiceDate = invoiceDate,
                DueDate = invoiceDate,
                SubTotal = plan.Price,
                TaxableAmount = plan.Price,
                CgstPercent = cgstPercent,
                SgstPercent = sgstPercent,
                CgstAmount = cgst,
                SgstAmount = sgst,
                TotalAmount = total,
                PaidAmount = total,
                Status = InvoiceStatus.Paid,
                PaymentId = payment.PaymentId,
                PaidUtc = nowUtc,
                CustomerName = customer.FullName,
                CustomerEmail = customer.Email,
                CustomerMobile = customer.MobileNumber,
                CustomerGstin = customer.Gstin,
                CustomerCompanyName = customer.CompanyName,
                BillingAddress = customer.Address,
                CompanyName = await settings.GetStringAsync(AppSettingKeys.Company.Name, "Regalis WorkSpace", ct),
                CompanyGstin = await settings.GetStringAsync(AppSettingKeys.Company.Gstin, "", ct),
                CompanyAddress = await settings.GetStringAsync(AppSettingKeys.Company.Address, "", ct)
            };
            invoice.Lines.Add(new InvoiceLine
            {
                LineNumber = 1,
                Description = $"{plan.PlanName} ({pass.PassNumber}) — valid " +
                              $"{pass.ValidFrom:dd MMM yyyy} to {pass.ValidTo:dd MMM yyyy}",
                Quantity = 1,
                UnitPrice = plan.Price,
                LineAmount = plan.Price,
                TaxPercent = cgstPercent + sgstPercent,
                TaxAmount = cgst + sgst,
                TotalAmount = total
            });
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(ct);
            pass.InvoiceId = invoice.InvoiceId;
            payment.InvoiceId = invoice.InvoiceId;

            // Access token valid across the whole pass window (IST days → UTC).
            var tokenValue = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var accessToken = new AccessToken
            {
                TokenValue = tokenValue,
                QrPayload = QrPayloadSigner.Sign(tokenValue, jwtOptions.Value.SigningKey),
                UserId = customerId,
                CustomerPassId = pass.CustomerPassId,
                LocationId = locationId,
                TokenPurpose = plan.PassType == PassType.DayPass
                    ? AccessTokenPurpose.DayPass
                    : AccessTokenPurpose.MonthlyPass,
                ValidFromUtc = IstClock.ToUtc(pass.ValidFrom.ToDateTime(TimeOnly.MinValue)),
                ValidToUtc = IstClock.ToUtc(pass.ValidTo.AddDays(1).ToDateTime(TimeOnly.MinValue)),
                Status = AccessTokenStatus.Active
            };
            db.AccessTokens.Add(accessToken);

            db.Notifications.Add(new Notification
            {
                UserId = customerId,
                Audience = NotificationAudience.SingleUser,
                Title = "Pass activated",
                Message = $"{plan.PlanName} ({pass.PassNumber}) is valid " +
                          $"{pass.ValidFrom:dd MMM} – {pass.ValidTo:dd MMM yyyy}. " +
                          "Your QR access pass is ready.",
                Category = NotificationCategory.Pass,
                Priority = NotificationPriority.Normal,
                ReferenceType = "CustomerPass",
                ReferenceId = pass.CustomerPassId,
                ActionLabel = "View passes",
                ActionRoute = "/passes",
                SentUtc = nowUtc
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new PurchasePassResponse(
                ToDto(pass, customer.FullName, plan.PlanName),
                payment.PaymentNumber, invoice.InvoiceId, invoice.InvoiceNumber,
                accessToken.TokenValue, accessToken.QrPayload);
        });

        logger.LogInformation("Pass {PassNumber} sold to customer {CustomerId} ({Method})",
            response.Pass.PassNumber, customerId, request.PaymentMethod);
        return response;
    }

    public async Task<PagedResult<CustomerPassDto>> GetMyPassesAsync(PassListRequest request,
        CancellationToken ct = default)
    {
        return await PageAsync(
            db.CustomerPasses.AsNoTracking().Where(p => p.CustomerId == currentUser.UserId),
            request, ct);
    }

    public async Task<PagedResult<CustomerPassDto>> GetPassesAsync(PassListRequest request,
        CancellationToken ct = default)
    {
        var query = db.CustomerPasses.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(p => p.PassNumber.Contains(term));
        }
        return await PageAsync(query, request, ct);
    }

    public async Task<CustomerPassDto> CancelAsync(long customerPassId, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var pass = await db.CustomerPasses
                .FirstOrDefaultAsync(p => p.CustomerPassId == customerPassId, ct)
                ?? throw new NotFoundException("CustomerPass", customerPassId);
            if (currentUser.UserType == UserType.Customer && pass.CustomerId != currentUser.UserId)
            {
                throw new NotFoundException("CustomerPass", customerPassId);
            }
            if (pass.Status != CustomerPassStatus.Active)
            {
                throw new BusinessRuleException("Only active passes can be cancelled.");
            }

            var todayIst = IstClock.IstDateOf(clock.UtcNow);
            // Full refund before the pass starts; once started there is no refund.
            if (pass.ValidFrom > todayIst)
            {
                await walletService.CreditAsync(pass.CustomerId, pass.TotalAmount,
                    WalletCategory.Refund, $"Refund for cancelled pass {pass.PassNumber}",
                    referenceType: "CustomerPass", referenceId: pass.CustomerPassId, ct: ct);
            }
            pass.Status = CustomerPassStatus.Cancelled;

            var tokens = await db.AccessTokens
                .Where(t => t.CustomerPassId == pass.CustomerPassId &&
                            t.Status == AccessTokenStatus.Active)
                .ToListAsync(ct);
            foreach (var token in tokens)
            {
                token.Status = AccessTokenStatus.Revoked;
                token.RevokedReason = "PassCancelled";
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        });

        var updated = await db.CustomerPasses.AsNoTracking()
            .Include(p => p.PassPlan)
            .FirstAsync(p => p.CustomerPassId == customerPassId, ct);
        var customerName = await db.Users.Where(u => u.UserId == updated.CustomerId)
            .Select(u => u.FullName).FirstAsync(ct);
        return ToDto(updated, customerName, updated.PassPlan.PlanName);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<PagedResult<CustomerPassDto>> PageAsync(IQueryable<CustomerPass> query,
        PassListRequest request, CancellationToken ct)
    {
        if (request.PassType is not null)
        {
            query = query.Where(p => (byte)p.PassType == request.PassType);
        }
        if (request.Status is not null)
        {
            query = query.Where(p => (byte)p.Status == request.Status);
        }
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CustomerPassId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(p => p.PassPlan)
            .Join(db.Users, p => p.CustomerId, u => u.UserId, (p, u) => new { Pass = p, u.FullName })
            .ToListAsync(ct);
        return PagedResult<CustomerPassDto>.Create(
            items.Select(x => ToDto(x.Pass, x.FullName, x.Pass.PassPlan.PlanName)).ToList(),
            request.Page, request.PageSize, totalCount);
    }

    private async Task<PassPlan> RequireActivePlanAsync(int passPlanId, CancellationToken ct) =>
        await db.PassPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PassPlanId == passPlanId && p.IsActive, ct)
        ?? throw new NotFoundException("PassPlan", passPlanId);

    private async Task<(decimal Cgst, decimal Sgst)> TaxPercentsAsync(CancellationToken ct) => (
        await settings.GetDecimalAsync(AppSettingKeys.Tax.CgstPercent, 9m, ct),
        await settings.GetDecimalAsync(AppSettingKeys.Tax.SgstPercent, 9m, ct));

    private static (decimal Cgst, decimal Sgst, decimal Total) PriceWithGst(
        decimal price, decimal cgstPercent, decimal sgstPercent)
    {
        var cgst = Math.Round(price * cgstPercent / 100m, 2, MidpointRounding.AwayFromZero);
        var sgst = Math.Round(price * sgstPercent / 100m, 2, MidpointRounding.AwayFromZero);
        return (cgst, sgst, price + cgst + sgst);
    }

    private static CustomerPassDto ToDto(CustomerPass p, string customerName, string planName) => new(
        p.CustomerPassId, p.PassNumber, p.CustomerId, customerName, p.PassPlanId, planName,
        (byte)p.PassType, p.LocationId, p.ValidFrom, p.ValidTo, p.Price, p.TaxAmount,
        p.TotalAmount, (byte)p.Status, p.PurchaseUtc, p.InvoiceId);
}
