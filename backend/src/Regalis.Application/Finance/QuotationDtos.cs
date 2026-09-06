using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance;

public record QuotationLineDto(
    long QuotationLineId,
    int LineNumber,
    int? WorkspaceTypeId,
    string? WorkspaceTypeName,
    int? ServiceItemId,
    string? ServiceItemName,
    string Description,
    string? HsnSacCode,
    decimal Quantity,
    decimal? DurationValue,
    string? DurationUnit,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal LineAmount,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal TotalAmount);

public record QuotationStatusHistoryDto(
    byte? FromStatus,
    byte ToStatus,
    long ChangedByUserId,
    string ChangedByName,
    DateTime ChangedUtc,
    string? Reason);

public record QuotationDto(
    long QuotationId,
    string QuotationNumber,
    long CustomerId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerMobile,
    string? CustomerGstin,
    string? BillingAddress,
    int LocationId,
    string? LocationName,
    DateOnly QuotationDate,
    DateOnly ValidUntil,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal CgstPercent,
    decimal CgstAmount,
    decimal SgstPercent,
    decimal SgstAmount,
    decimal IgstPercent,
    decimal IgstAmount,
    decimal TotalAmount,
    // 1 Draft, 2 Sent, 3 Viewed, 4 Approved, 5 Rejected, 6 Expired, 7 Cancelled.
    byte Status,
    DateTime? SentUtc,
    DateTime? ViewedUtc,
    DateTime? RespondedUtc,
    long? RespondedByUserId,
    string? RejectionReason,
    long? InvoiceId,
    string? InvoiceNumber,
    string CompanyName,
    string? CompanyGstin,
    string? CompanyAddress,
    string? PlaceOfSupply,
    string? TermsAndConditions,
    string? Remarks,
    // True once past ValidUntil: computed, never stored, so a quotation reads as
    // expired the moment it lapses rather than when the sweeper next runs.
    bool IsExpired,
    DateTime CreatedDate,
    List<QuotationLineDto> Lines,
    List<QuotationStatusHistoryDto> StatusHistory);

public record SaveQuotationLineRequest(
    int? WorkspaceTypeId,
    int? ServiceItemId,
    string Description,
    string? HsnSacCode,
    decimal Quantity,
    decimal? DurationValue,
    string? DurationUnit,
    decimal UnitPrice,
    decimal DiscountAmount);

public record SaveQuotationRequest(
    long CustomerId,
    int LocationId,
    DateOnly? QuotationDate,
    DateOnly? ValidUntil,
    string? TermsAndConditions,
    string? Remarks,
    List<SaveQuotationLineRequest> Lines);

/// <summary>The customer's decision. A reason is optional on approval and on
/// rejection alike — the document only asks that it be captured when given.</summary>
public record QuotationDecisionRequest(string? Reason);

public class QuotationListRequest : PagedRequest
{
    public byte? Status { get; set; }
    public long? CustomerId { get; set; }
    public int? LocationId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class SaveQuotationLineRequestValidator : AbstractValidator<SaveQuotationLineRequest>
{
    public SaveQuotationLineRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        RuleFor(x => x.HsnSacCode).MaximumLength(20);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DurationValue).GreaterThan(0).When(x => x.DurationValue is not null);
        RuleFor(x => x.DurationUnit).MaximumLength(20);
        RuleFor(x => x.WorkspaceTypeId).GreaterThan(0).When(x => x.WorkspaceTypeId is not null);
        RuleFor(x => x.ServiceItemId).GreaterThan(0).When(x => x.ServiceItemId is not null);
        RuleFor(x => x)
            .Must(x => x.DiscountAmount <= x.Quantity * x.UnitPrice)
            .WithMessage("Line discount cannot exceed the line value.")
            .WithName(nameof(SaveQuotationLineRequest.DiscountAmount));
        // A duration without its unit is unreadable on the printed quotation.
        RuleFor(x => x.DurationUnit).NotEmpty()
            .When(x => x.DurationValue is not null)
            .WithMessage("Give the duration a unit, e.g. Hours, Days or Months.");
    }
}

public class SaveQuotationRequestValidator : AbstractValidator<SaveQuotationRequest>
{
    public SaveQuotationRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.LocationId).GreaterThan(0);
        RuleFor(x => x.TermsAndConditions).MaximumLength(2000);
        RuleFor(x => x.Remarks).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("A quotation needs at least one line.");
        RuleForEach(x => x.Lines).SetValidator(new SaveQuotationLineRequestValidator());
        RuleFor(x => x)
            .Must(x => x.ValidUntil is null || x.QuotationDate is null || x.ValidUntil >= x.QuotationDate)
            .WithMessage("The validity date cannot be before the quotation date.")
            .WithName(nameof(SaveQuotationRequest.ValidUntil));
    }
}

public class QuotationDecisionRequestValidator : AbstractValidator<QuotationDecisionRequest>
{
    public QuotationDecisionRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

/// <summary>
/// Quotations (document §6–§8). A quotation is freely editable while Draft; sending it
/// snapshots the customer, company and tax details, and from that point the figures the
/// customer saw are the figures that stand. Approval and rejection are the customer's
/// own act, recorded with their identity and the moment they made it.
/// </summary>
public interface IQuotationService
{
    Task<PagedResult<QuotationDto>> GetQuotationsAsync(QuotationListRequest request,
        CancellationToken ct = default);

    /// <summary>Admin read. Customers must use <see cref="GetMyQuotationAsync"/>.</summary>
    Task<QuotationDto> GetQuotationAsync(long quotationId, CancellationToken ct = default);

    /// <summary>The caller's own quotations, whatever id is supplied.</summary>
    Task<PagedResult<QuotationDto>> GetMyQuotationsAsync(QuotationListRequest request,
        CancellationToken ct = default);

    /// <summary>Reading a sent quotation as the customer moves it to Viewed.</summary>
    Task<QuotationDto> GetMyQuotationAsync(long quotationId, CancellationToken ct = default);

    Task<QuotationDto> CreateAsync(SaveQuotationRequest request, CancellationToken ct = default);

    /// <summary>Draft only — a quotation the customer has seen is frozen.</summary>
    Task<QuotationDto> UpdateAsync(long quotationId, SaveQuotationRequest request,
        CancellationToken ct = default);

    /// <summary>Draft to Sent: snapshots customer, company, place of supply and the
    /// tax rates, and makes the quotation visible in the customer portal.</summary>
    Task<QuotationDto> SendAsync(long quotationId, CancellationToken ct = default);

    Task<QuotationDto> ApproveAsync(long quotationId, QuotationDecisionRequest request,
        CancellationToken ct = default);

    Task<QuotationDto> RejectAsync(long quotationId, QuotationDecisionRequest request,
        CancellationToken ct = default);

    Task<QuotationDto> CancelAsync(long quotationId, string reason, CancellationToken ct = default);

    /// <summary>The quotation as a PDF, generated from the snapshot.</summary>
    Task<byte[]> GetQuotationPdfAsync(long quotationId, CancellationToken ct = default);
}
