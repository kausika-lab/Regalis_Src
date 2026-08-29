using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Adapters;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

public class PaymentService(
    RegalisDbContext db,
    IPaymentGateway paymentGateway,
    INumberSequenceService sequences,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IConfiguration configuration,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<Shared.Api.PagedResult<PaymentDto>> GetPaymentsAsync(PaymentListRequest request,
        CancellationToken ct = default)
    {
        var query = db.Payments.AsNoTracking();
        if (request.Status is not null)
        {
            query = query.Where(p => (byte)p.Status == request.Status);
        }
        if (request.PaymentMethod is not null)
        {
            query = query.Where(p => (byte)p.PaymentMethod == request.PaymentMethod);
        }
        if (request.CustomerId is not null)
        {
            query = query.Where(p => p.CustomerId == request.CustomerId);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(p => p.PaymentNumber.Contains(term) ||
                                     (p.GatewayOrderId != null && p.GatewayOrderId.Contains(term)));
        }
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.PaymentId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Join(db.Users, p => p.CustomerId, u => u.UserId, (p, u) => new { p, u.FullName })
            .Select(x => new PaymentDto(
                x.p.PaymentId, x.p.PaymentNumber, x.p.CustomerId, x.FullName, x.p.BookingId,
                x.p.InvoiceId, x.p.CustomerPassId, x.p.Amount, (byte)x.p.PaymentMethod, x.p.Gateway,
                x.p.GatewayOrderId, x.p.IsSignatureVerified, (byte)x.p.Status, x.p.PaidUtc,
                x.p.FailureReason, x.p.CreatedDate))
            .ToListAsync(ct);
        return Shared.Api.PagedResult<PaymentDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<PaymentDto> GetPaymentAsync(long paymentId, CancellationToken ct = default)
    {
        var payment = await db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId, ct)
            ?? throw new NotFoundException("Payment", paymentId);
        if (currentUser.UserType == UserType.Customer && payment.CustomerId != currentUser.UserId)
        {
            throw new NotFoundException("Payment", paymentId);
        }
        var customerName = await db.Users.Where(u => u.UserId == payment.CustomerId)
            .Select(u => u.FullName).FirstAsync(ct);
        return new PaymentDto(payment.PaymentId, payment.PaymentNumber, payment.CustomerId,
            customerName, payment.BookingId, payment.InvoiceId, payment.CustomerPassId,
            payment.Amount, (byte)payment.PaymentMethod, payment.Gateway, payment.GatewayOrderId,
            payment.IsSignatureVerified, (byte)payment.Status, payment.PaidUtc,
            payment.FailureReason, payment.CreatedDate);
    }

    public async Task<PaymentOrderResponse> CreateOrderAsync(CreatePaymentOrderRequest request,
        CancellationToken ct = default)
    {
        var booking = await db.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingId == request.BookingId, ct)
            ?? throw new NotFoundException("Booking", request.BookingId);
        if (currentUser.UserType == UserType.Customer && booking.CustomerId != currentUser.UserId)
        {
            throw new NotFoundException("Booking", request.BookingId);
        }
        if (booking.Status != BookingStatus.Pending)
        {
            throw new BusinessRuleException("This booking is not awaiting payment.");
        }

        var order = await paymentGateway.CreateOrderAsync(
            booking.TotalAmount, "INR", booking.BookingNumber, ct);

        var strategy = db.Database.CreateExecutionStrategy();
        var payment = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var entity = new Payment
            {
                PaymentNumber = await sequences.NextAsync(SequenceKeys.Payment, ct),
                CustomerId = booking.CustomerId,
                BookingId = booking.BookingId,
                Amount = booking.TotalAmount,
                PaymentMethod = PaymentMethod.Upi,
                Gateway = paymentGateway.ProviderName,
                GatewayOrderId = order.OrderId,
                Status = PaymentStatus.Pending
            };
            db.Payments.Add(entity);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return entity;
        });

        return new PaymentOrderResponse(payment.PaymentId, payment.PaymentNumber,
            order.OrderId, order.KeyId, order.Amount, order.Currency, paymentGateway.ProviderName);
    }

    public async Task<SandboxCompleteResponse> SandboxCompleteAsync(SandboxCompleteRequest request,
        CancellationToken ct = default)
    {
        if (paymentGateway is not SandboxPaymentGateway sandbox)
        {
            throw new BusinessRuleException(
                "The sandbox checkout is available only while Payments:Provider is Sandbox.");
        }
        var exists = await db.Payments.AnyAsync(p => p.GatewayOrderId == request.GatewayOrderId, ct);
        if (!exists)
        {
            throw new NotFoundException("PaymentOrder", request.GatewayOrderId);
        }
        var paymentId = $"pay_SBX{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
        return new SandboxCompleteResponse(paymentId, sandbox.Sign(request.GatewayOrderId, paymentId));
    }

    public async Task HandleWebhookAsync(string body, string? signature, CancellationToken ct = default)
    {
        // Razorpay signs the raw body with the webhook secret; without a valid
        // signature the payload is ignored (and logged) — never trusted.
        var webhookSecret = configuration["Payments:Razorpay:WebhookSecret"] ?? string.Empty;
        if (string.IsNullOrEmpty(webhookSecret) || string.IsNullOrEmpty(signature))
        {
            logger.LogWarning("Payment webhook rejected: missing secret or signature");
            throw new UnauthorizedException("Invalid webhook signature.",
                ErrorCodes.PaymentVerificationFailed);
        }
        var expected = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(webhookSecret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature)))
        {
            logger.LogWarning("Payment webhook rejected: signature mismatch");
            throw new UnauthorizedException("Invalid webhook signature.",
                ErrorCodes.PaymentVerificationFailed);
        }

        using var document = JsonDocument.Parse(body);
        var paymentEntity = document.RootElement
            .GetProperty("payload").GetProperty("payment").GetProperty("entity");
        var gatewayPaymentId = paymentEntity.GetProperty("id").GetString();
        var gatewayOrderId = paymentEntity.GetProperty("order_id").GetString();

        var payment = await db.Payments
            .FirstOrDefaultAsync(p => p.GatewayOrderId == gatewayOrderId, ct);
        if (payment is null)
        {
            logger.LogWarning("Webhook for unknown order {OrderId} ignored", gatewayOrderId);
            return;
        }
        // Idempotent: a payment already settled inline (or by an earlier delivery)
        // is left untouched — UX_Payment_GatewayPaymentId guards the double-write.
        if (payment.Status == PaymentStatus.Pending)
        {
            payment.GatewayPaymentId = gatewayPaymentId;
            payment.Status = PaymentStatus.Success;
            payment.PaidUtc = clock.UtcNow;
            payment.RawGatewayResponse = body;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Webhook settled payment {PaymentNumber}", payment.PaymentNumber);
        }
    }
}
