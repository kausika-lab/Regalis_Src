using System;
using System.Collections.Generic;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance;

/// <summary>Can this booking be paid later, and if not, why (shown at checkout).</summary>
public record PayLaterEligibilityDto(bool Allowed, string? Reason, decimal AmountDue, DateTime? DueUtc);

/// <summary>Before a hold: may the signed-in customer pay later, and for which kinds
/// (Hourly, MultiDay, Monthly). Drives whether checkout shows the option.</summary>
public record PayLaterStatusDto(bool Available, string? Reason, IReadOnlyList<string> AllowedKinds);

/// <summary>A pay-later booking, day pass or membership that still owes money. Kind is
/// "Booking" or "Pass"; for a pass BookingId is 0, CustomerPassId is set, BookingNumber
/// carries the pass number, WorkspaceName the plan, and Start/End its first and last day.</summary>
public record OutstandingPayLaterDto(
    long BookingId,
    string BookingNumber,
    long CustomerId,
    string CustomerName,
    string CustomerMobile,
    int LocationId,
    string WorkspaceName,
    DateTime StartUtc,
    DateTime EndUtc,
    byte? BillingMonths,
    byte Status,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Balance,
    DateTime? PaymentDueUtc,
    bool IsOverdue,
    long? InvoiceId,
    string? InvoiceNumber,
    string Kind = "Booking",
    long? CustomerPassId = null);

public class OutstandingListRequest : PagedRequest
{
    public int? LocationId { get; set; }
    public bool OverdueOnly { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

/// <summary>Money taken at the desk. PaymentMethod: 6 cash, 2 UPI, 3 credit card,
/// 4 debit card. A cash receipt may carry what was tendered (change is worked out);
/// UPI and card need the transaction or slip reference.</summary>
/// Exactly one of BookingId and CustomerPassId.
public record RecordCollectionRequest(
    long? BookingId,
    decimal Amount,
    byte PaymentMethod,
    decimal? AmountTendered,
    string? ReferenceNumber,
    string? Notes,
    long? CustomerPassId = null);

public record VoidCollectionRequest(string Reason);

/// <summary>BookingId is 0 for a pass receipt (then CustomerPassId is set and
/// BookingNumber carries the pass number).</summary>
public record CashCollectionDto(
    long CashCollectionId,
    string ReceiptNumber,
    long BookingId,
    string BookingNumber,
    long CustomerId,
    string CustomerName,
    int LocationId,
    long? InvoiceId,
    string? InvoiceNumber,
    long? PaymentId,
    string? PaymentNumber,
    byte PaymentMethod,
    decimal Amount,
    decimal? AmountTendered,
    decimal? ChangeReturned,
    string? ReferenceNumber,
    long CollectedByUserId,
    string CollectedByName,
    DateTime CollectedUtc,
    long? CashRegisterSessionId,
    string? SessionNumber,
    byte Status,
    string? VoidedByName,
    DateTime? VoidedUtc,
    string? VoidReason,
    string? Notes,
    decimal BookingBalance,
    long? CustomerPassId = null);

public class CashCollectionListRequest : PagedRequest
{
    public int? LocationId { get; set; }
    public long? CollectedByUserId { get; set; }
    public long? BookingId { get; set; }
    public long? CustomerPassId { get; set; }
    public long? CashRegisterSessionId { get; set; }
    public byte? PaymentMethod { get; set; }
    public byte? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public record OpenRegisterRequest(int LocationId, decimal OpeningFloat);

public record CloseRegisterRequest(decimal CountedCash, string? VarianceReason);

public record CashMethodTotalDto(byte PaymentMethod, int Count, decimal Amount);

public record CashRegisterSessionDto(
    long CashRegisterSessionId,
    string SessionNumber,
    int LocationId,
    long UserId,
    string UserName,
    DateOnly BusinessDate,
    decimal OpeningFloat,
    DateTime OpenedUtc,
    decimal CashCollected,
    decimal ExpectedCash,
    decimal? CountedCash,
    decimal? Variance,
    string? VarianceReason,
    byte Status,
    DateTime? ClosedUtc,
    long? HandedOverToUserId,
    string? HandedOverToName,
    DateTime? HandedOverUtc,
    IReadOnlyList<CashMethodTotalDto> Totals);

public class CashRegisterListRequest : PagedRequest
{
    public int? LocationId { get; set; }
    public long? UserId { get; set; }
    public byte? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
