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
using Regalis.Domain.Entities.Operations;
using Regalis.Infrastructure.Reporting;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

public class PaymentService(
    RegalisDbContext db,
    IPaymentGateway paymentGateway,
    ISettingService settings,
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
                x.p.InvoiceId,
                db.Invoices.Where(i => i.InvoiceId == x.p.InvoiceId)
                    .Select(i => i.InvoiceNumber).FirstOrDefault(),
                db.Invoices.Where(i => i.InvoiceId == x.p.InvoiceId)
                    .Select(i => (decimal?)i.TotalAmount).FirstOrDefault(),
                x.p.CustomerPassId, x.p.Amount, (byte)x.p.PaymentMethod, x.p.Gateway,
                x.p.GatewayOrderId, x.p.GatewayPaymentId, x.p.RefundedAmount, x.p.RefundReference,
                x.p.IsSignatureVerified, (byte)x.p.Status, x.p.PaidUtc,
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
        var invoice = payment.InvoiceId is null
            ? null
            : await db.Invoices.AsNoTracking()
                .Where(i => i.InvoiceId == payment.InvoiceId)
                .Select(i => new { i.InvoiceNumber, i.TotalAmount })
                .FirstOrDefaultAsync(ct);

        return new PaymentDto(payment.PaymentId, payment.PaymentNumber, payment.CustomerId,
            customerName, payment.BookingId, payment.InvoiceId,
            invoice?.InvoiceNumber, invoice?.TotalAmount, payment.CustomerPassId,
            payment.Amount, (byte)payment.PaymentMethod, payment.Gateway, payment.GatewayOrderId,
            payment.GatewayPaymentId, payment.RefundedAmount, payment.RefundReference,
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

    public async Task<PaymentOrderResponse> CreateInvoiceOrderAsync(
        CreateInvoicePaymentOrderRequest request, CancellationToken ct = default)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceId == request.InvoiceId, ct)
            ?? throw new NotFoundException("Invoice", request.InvoiceId);

        // Row scoping: a customer can only ever open an order against their own invoice,
        // whatever id they send. 404 rather than 403 — they learn nothing either way.
        if (currentUser.UserType == UserType.Customer && invoice.CustomerId != currentUser.UserId)
        {
            throw new NotFoundException("Invoice", request.InvoiceId);
        }
        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new BusinessRuleException("This invoice was cancelled.");
        }
        if (invoice.OutstandingAmount <= 0)
        {
            throw new BusinessRuleException("This invoice is already settled.");
        }

        // Default to the whole balance; a smaller amount leaves it PartiallyPaid.
        var amount = request.Amount ?? invoice.OutstandingAmount;
        if (amount > invoice.OutstandingAmount)
        {
            throw new BusinessRuleException(
                $"Only {invoice.OutstandingAmount:N2} is outstanding on {invoice.InvoiceNumber}.");
        }

        var order = await paymentGateway.CreateOrderAsync(amount, "INR", invoice.InvoiceNumber, ct);

        var strategy = db.Database.CreateExecutionStrategy();
        var payment = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var entity = new Payment
            {
                PaymentNumber = await sequences.NextAsync(SequenceKeys.Payment, ct),
                CustomerId = invoice.CustomerId,
                InvoiceId = invoice.InvoiceId,
                Amount = amount,
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

    public async Task<InvoicePaymentResult> VerifyInvoicePaymentAsync(
        VerifyInvoicePaymentRequest request, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        var paymentId = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var payment = await db.Payments.FirstOrDefaultAsync(p =>
                p.GatewayOrderId == request.GatewayOrderId &&
                p.CustomerId == currentUser.UserId &&
                p.InvoiceId != null, ct)
                ?? throw new BusinessRuleException("No payment order was found for this invoice.",
                    ErrorCodes.PaymentVerificationFailed);

            if (payment.Status != PaymentStatus.Pending)
            {
                throw new BusinessRuleException("This payment is already settled.",
                    ErrorCodes.PaymentVerificationFailed);
            }

            // A failed signature marks the payment Failed and leaves the invoice alone:
            // the customer can retry, and nothing was ever marked Paid (document §11).
            if (!paymentGateway.VerifySignature(request.GatewayOrderId,
                    request.GatewayPaymentId, request.GatewaySignature))
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = "Signature verification failed";
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                logger.LogWarning("Invoice payment {PaymentNumber} failed signature verification",
                    payment.PaymentNumber);
                throw new BusinessRuleException("Payment verification failed.",
                    ErrorCodes.PaymentVerificationFailed);
            }

            var invoice = await db.Invoices
                .FirstAsync(i => i.InvoiceId == payment.InvoiceId!.Value, ct);

            // Re-check under the transaction: another payment may have landed since the
            // order was opened, and the invoice must never end up over-collected.
            if (payment.Amount > invoice.OutstandingAmount)
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = "Amount exceeds the outstanding balance";
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                throw new BusinessRuleException(
                    $"Only {invoice.OutstandingAmount:N2} is outstanding on {invoice.InvoiceNumber}. "
                    + "The payment was not applied.",
                    ErrorCodes.PaymentVerificationFailed);
            }

            payment.GatewayPaymentId = request.GatewayPaymentId;
            payment.GatewaySignature = request.GatewaySignature;
            payment.IsSignatureVerified = true;
            payment.Status = PaymentStatus.Success;
            payment.PaidUtc = clock.UtcNow;

            // OutstandingAmount is a computed column, so only PaidAmount is written.
            invoice.PaidAmount += payment.Amount;
            var settled = invoice.PaidAmount >= invoice.TotalAmount;
            invoice.Status = settled ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
            if (settled)
            {
                invoice.PaidUtc = clock.UtcNow;
                invoice.PaymentId = payment.PaymentId;
            }

            db.Notifications.Add(new Notification
            {
                UserId = invoice.CustomerId,
                Audience = NotificationAudience.SingleUser,
                Title = settled ? "Payment received" : "Part payment received",
                Message = $"Rs {payment.Amount:N2} received against invoice {invoice.InvoiceNumber}. "
                          + (settled
                              ? "It is now paid in full."
                              : $"Rs {(invoice.TotalAmount - invoice.PaidAmount):N2} remains outstanding."),
                Category = NotificationCategory.Payment,
                Priority = NotificationPriority.Normal,
                ReferenceType = "Payment",
                ReferenceId = payment.PaymentId,
                ActionLabel = "View invoice",
                ActionRoute = "/invoices",
                SentUtc = clock.UtcNow
            });

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return payment.PaymentId;
        });

        var settledPayment = await GetPaymentAsync(paymentId, ct);
        var updated = await db.Invoices.AsNoTracking()
            .FirstAsync(i => i.InvoiceId == settledPayment.InvoiceId!.Value, ct);

        logger.LogInformation("Invoice {InvoiceNumber} received {Amount}",
            updated.InvoiceNumber, settledPayment.Amount);

        return new InvoicePaymentResult(settledPayment, updated.InvoiceId, updated.InvoiceNumber,
            updated.PaidAmount, updated.OutstandingAmount, (byte)updated.Status);
    }

    public async Task<byte[]> GetReceiptPdfAsync(long paymentId, CancellationToken ct = default)
    {
        var payment = await db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId, ct)
            ?? throw new NotFoundException("Payment", paymentId);
        if (currentUser.UserType == UserType.Customer && payment.CustomerId != currentUser.UserId)
        {
            throw new NotFoundException("Payment", paymentId);
        }
        if (payment.Status != PaymentStatus.Success)
        {
            throw new BusinessRuleException("A receipt exists only for a successful payment.");
        }

        var customerName = await db.Users.Where(u => u.UserId == payment.CustomerId)
            .Select(u => u.FullName).FirstAsync(ct);
        var invoice = payment.InvoiceId is null
            ? null
            : await db.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == payment.InvoiceId, ct);

        var companyName = await settings.GetStringAsync(
            AppSettingKeys.Company.Name, "Regalis WorkSpace", ct);
        var companyAddress = await settings.GetStringAsync(AppSettingKeys.Company.Address, "", ct);
        var companyGstin = await settings.GetStringAsync(AppSettingKeys.Company.Gstin, "", ct);

        return ReceiptPdfGenerator.Generate(payment, customerName, invoice,
            companyName, companyAddress, companyGstin);
    }
}
