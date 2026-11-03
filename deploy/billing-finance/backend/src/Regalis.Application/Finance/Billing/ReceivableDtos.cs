using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance.Billing;

/// <summary>Record payment dialog (build doc §3.3). Mode: 1 Cash, 2 UPI, 3 Bank transfer,
/// 4 Cheque, 5 Card, 6 Wallet.</summary>
public record RecordInvoicePaymentRequest(decimal Amount, DateOnly PaidOn, byte Mode, string? Reference, int? BankAccountId, string? Note);

public record ReversePaymentRequest(string Reason);

public record InvoicePaymentDto(
    long InvoicePaymentId, string ReceiptNumber, long InvoiceId, string InvoiceNumber, long CustomerId, string CustomerName,
    long? PaymentId, string? PaymentNumber, decimal Amount, DateOnly PaidOn, byte Mode, string ModeName, string? Reference,
    int? BankAccountId, string? BankAccountName, string? Note, bool IsReversed, string? ReverseReason, DateTime? ReversedUtc,
    long? ReversalOfId, string? RecordedBy, DateTime RecordedUtc, bool IsReconciled);

public record RecordInvoicePaymentResult(InvoicePaymentDto Payment, string InvoiceNumber, decimal InvoiceTotal, decimal PaidAmount,
    decimal Outstanding, byte InvoiceStatus, string Message);

public record BankAccountOptionDto(int BankAccountId, string Label);

/// <summary>Everything the dialog shows before anything is typed.</summary>
public record InvoicePaymentContextDto(
    long InvoiceId, string InvoiceNumber, string CustomerName, DateOnly InvoiceDate, decimal InvoiceTotal, decimal PaidAmount,
    decimal Outstanding, byte InvoiceStatus, decimal WalletBalance, string NextReceiptNumber, bool PeriodLockedToday,
    IReadOnlyList<BankAccountOptionDto> BankAccounts, IReadOnlyList<InvoicePaymentDto> Payments);

public class PaymentRegisterRequest : PagedRequest
{
    /// <summary>all, online, offline.</summary>
    public string? Source { get; set; }
    public byte? Status { get; set; }
    public byte? Mode { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

/// <summary>One row of the Payments page: every payment whatever its mode (doc §3.6).</summary>
public record PaymentRegisterRowDto(
    long PaymentId, string PaymentNumber, long CustomerId, string CustomerName, decimal Amount, byte PaymentMethod, byte Status,
    string? Gateway, string? GatewayOrderId, string? GatewayPaymentId, DateTime? PaidUtc, DateTime CreatedDate,
    bool IsOffline, string ModeName, string? Reference, long? InvoiceId, string? InvoiceNumber, long? InvoicePaymentId,
    string? ReceiptNumber, bool IsReversed, string? ReverseReason, bool CanReverse, bool IsReconciled);

public record CustomerTaxProfileDto(long CustomerId, string? Tan, bool DoNotChase, string? DoNotChaseReason);

public record SaveCustomerTaxProfileRequest(string? Tan, bool DoNotChase, string? DoNotChaseReason);

public interface ICustomerPaymentService
{
    Task<InvoicePaymentContextDto> GetContextAsync(long invoiceId, CancellationToken ct = default);

    Task<IReadOnlyList<InvoicePaymentDto>> GetForInvoiceAsync(long invoiceId, CancellationToken ct = default);

    /// <summary>Records a non-gateway payment against an invoice in one transaction.</summary>
    Task<RecordInvoicePaymentResult> RecordAsync(long invoiceId, RecordInvoicePaymentRequest request, CancellationToken ct = default);

    /// <summary>Reverses the receipt behind a PAY- payment; writes the opposite entry.</summary>
    Task<InvoicePaymentDto> ReverseAsync(long paymentId, ReversePaymentRequest request, CancellationToken ct = default);

    Task<PagedResult<PaymentRegisterRowDto>> GetRegisterAsync(PaymentRegisterRequest request, CancellationToken ct = default);

    Task<string> EmailReceiptAsync(long paymentId, CancellationToken ct = default);

    Task<CustomerTaxProfileDto> GetCustomerTaxProfileAsync(long customerId, CancellationToken ct = default);

    Task<CustomerTaxProfileDto> SaveCustomerTaxProfileAsync(long customerId, SaveCustomerTaxProfileRequest request, CancellationToken ct = default);
}
