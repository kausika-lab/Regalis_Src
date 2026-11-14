using System;
using System.Threading;
using System.Threading.Tasks;
using Regalis.Shared.Api;

namespace Regalis.Application.Account;

/// <summary>One row of the customer's Payment History (customer-10 kit, item 10).
/// Status is the stored PaymentStatus (1 Pending, 2 Success, 3 Failed, 4 Refunded,
/// 5 Cancelled); DisplayStatus is what the customer sees: a Pending online checkout older
/// than 30 minutes reads "Not completed" (display only, nothing is changed).</summary>
public sealed record MyPaymentDto(
    long PaymentId,
    string PaymentNumber,
    string TransactionId,
    string? GatewayOrderId,
    string? GatewayPaymentId,
    string ReferenceType,
    string? ReferenceNumber,
    long? BookingId,
    long? CustomerPassId,
    long? InvoiceId,
    string? InvoiceNumber,
    string Description,
    decimal Amount,
    byte PaymentMethod,
    string MethodLabel,
    string? Gateway,
    byte Status,
    string DisplayStatus,
    DateTime CreatedUtc,
    DateTime? PaidUtc,
    string? FailureReason,
    decimal? RefundedAmount,
    string? RefundReference,
    bool HasReceipt);

public sealed record MyPaymentsQuery(int Page = 1, int PageSize = 20, string? Status = null);

public interface IMyPaymentsService
{
    Task<PagedResult<MyPaymentDto>> ListAsync(MyPaymentsQuery query, CancellationToken ct = default);

    Task<MyPaymentDto> GetAsync(long paymentId, CancellationToken ct = default);
}
