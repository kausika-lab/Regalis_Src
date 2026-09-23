using FluentValidation;
using Regalis.Application.Operations;
using Regalis.Domain.Enums;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance;

// ── Read models ─────────────────────────────────────────────────────────────

public record QuotationLineDto(
    long QuotationLineId,
    int LineNumber,
    // 1 Service, 2 Custom, 3 Header.
    byte LineType,
    // 1 Cabin, 2 Desk, 3 MeetingRoom, 4 Pass, 5 Other.
    byte ServiceType,
    int? WorkspaceTypeId,
    string? WorkspaceTypeName,
    int? ServiceItemId,
    string? ServiceItemName,
    int? WorkspaceId,
    int? PassPlanId,
    string Description,
    string? LineNotes,
    string? HsnSacCode,
    decimal Quantity,
    decimal? DurationValue,
    string? DurationUnit,
    decimal UnitPrice,
    decimal? StandardRate,
    decimal DiscountAmount,
    decimal DiscountShare,
    decimal LineAmount,
    decimal TaxableAmount,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal TotalAmount);

public record QuotationStatusHistoryDto(
    byte? FromStatus,
    byte ToStatus,
    long ChangedByUserId,
    string ChangedByName,
    DateTime ChangedUtc,
    string? Reason);

/// <summary>One line of the activity timeline (document §3.6).</summary>
public record QuotationEventDto(
    long QuotationEventId,
    byte EventType,
    byte ActorType,
    long? ActorUserId,
    string ActorName,
    string Summary,
    string? Channel,
    string? Destination,
    byte? DeliveryStatus,
    string? FailureReason,
    DateTime CreatedUtc);

public record QuotationAttachmentDto(
    long QuotationAttachmentId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    bool SendWithEmail,
    DateTime UploadedUtc);

/// <summary>The action matrix from document §3.6, resolved server-side. The screen
/// shows a button when the flag is true; the API enforces the same rule regardless.</summary>
public record QuotationActionsDto(
    bool CanEdit,
    bool CanSend,
    bool CanDownloadPdf,
    bool CanCopyPortalLink,
    bool CanRevise,
    bool CanClone,
    bool CanRecordDecision,
    bool CanConvertToInvoice,
    bool CanDelete,
    bool CanCancel);

public record QuotationDto(
    long QuotationId,
    string QuotationNumber,
    long CustomerId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerMobile,
    string? CustomerGstin,
    string? CustomerCompanyName,
    string? BillingAddress,
    int LocationId,
    string? LocationName,
    DateOnly QuotationDate,
    DateOnly ValidUntil,
    long? RevisionOfQuotationId,
    string? RevisionOfQuotationNumber,
    int RevisionNumber,
    long? ReplacedByQuotationId,
    string? ReplacedByQuotationNumber,
    string? ReferenceNumber,
    string? Subject,
    long? SalespersonId,
    string? SalespersonName,
    decimal SubTotal,
    // 1 Percentage, 2 Amount.
    byte DiscountType,
    decimal DiscountValue,
    decimal DiscountAmount,
    decimal TaxableAmount,
    // 1 IntraState (CGST+SGST), 2 InterState (IGST).
    byte TaxMode,
    decimal CgstPercent,
    decimal CgstAmount,
    decimal SgstPercent,
    decimal SgstAmount,
    decimal IgstPercent,
    decimal IgstAmount,
    string? TdsCode,
    string? TdsLabel,
    decimal TdsPercent,
    decimal TdsAmount,
    string? AdjustmentLabel,
    decimal AdjustmentAmount,
    decimal RoundOffAmount,
    decimal TotalAmount,
    string TotalInWords,
    // 1 Draft, 2 Sent, 3 Viewed, 4 Approved, 5 Rejected, 6 Expired, 7 Cancelled, 8 Revised.
    byte Status,
    DateTime? SentUtc,
    DateTime? ViewedUtc,
    DateTime? RespondedUtc,
    long? RespondedByUserId,
    string? RejectionReason,
    string? RejectionCode,
    bool DecisionOnBehalf,
    byte? DecisionMode,
    string? DecisionConfirmedBy,
    string? DecisionNote,
    long? InvoiceId,
    string? InvoiceNumber,
    bool AdvanceInvoiceEnabled,
    byte AdvanceType,
    decimal AdvanceValue,
    string CompanyName,
    string? CompanyGstin,
    string? CompanyAddress,
    string? PlaceOfSupply,
    string? PlaceOfSupplyStateCode,
    string? TermsAndConditions,
    string? Remarks,
    // Admin-only; the customer routes always return this null.
    string? InternalNote,
    // True once past ValidUntil: computed, never stored, so a quotation reads as
    // expired the moment it lapses rather than when the sweeper next runs.
    bool IsExpired,
    // Days left before it lapses; negative once it has. Drives the "in 8 days" hint
    // and the amber warning at 3 days or less (document §3.1).
    int DaysToExpiry,
    QuotationActionsDto Actions,
    DateTime CreatedDate,
    List<QuotationLineDto> Lines,
    List<QuotationStatusHistoryDto> StatusHistory,
    List<QuotationAttachmentDto> Attachments,
    List<QuotationEventDto> Events);

/// <summary>The four summary cards and the status tab counts (document §3.1).
/// Scoped to the current financial year, which is what "Total quotes" means there.</summary>
public record QuotationSummaryDto(
    string FinancialYear,
    int TotalCount,
    decimal TotalValue,
    int AwaitingCount,
    decimal AwaitingValue,
    int ApprovedCount,
    decimal ApprovedValue,
    int ExpiringSoonCount,
    decimal ExpiringSoonValue,
    // Count per status byte, for the tab labels. Keys are QuotationStatus.
    Dictionary<byte, int> StatusCounts);

/// <summary>One pickable service for the item table, with the price already configured
/// for it (document §2.1). Grouped client-side by <see cref="Group"/>.</summary>
public record QuotationServiceOptionDto(
    byte ServiceType,
    string Group,
    int? WorkspaceId,
    int? WorkspaceTypeId,
    int? PassPlanId,
    string Code,
    string Name,
    int? Capacity,
    // Hour / Day / Month / One-time — the unit this service is normally sold in.
    string DefaultDurationUnit,
    decimal? RatePerHour,
    decimal? RatePerDay,
    decimal? RatePerMonth,
    decimal? RateOneTime,
    // True for a specific cabin or meeting room: quantity is locked to 1 and a second
    // one goes on its own line (document §3.3).
    bool QuantityLocked);

public record QuotationTdsOptionDto(string Code, string Label, decimal Percent);

/// <summary>GST state for the place-of-supply picker. Code 29 is Karnataka, the one
/// that makes a quotation intra-state.</summary>
public record QuotationStateOptionDto(string Code, string Name);

/// <summary>Everything the new-quotation form needs in one call: the service picker,
/// the tax and TDS options, the defaults from settings and a preview of the number
/// the server will issue (document §3.2–§3.4).</summary>
public record QuotationFormOptionsDto(
    string NextQuotationNumber,
    int DefaultValidityDays,
    decimal DefaultGstPercent,
    IReadOnlyList<decimal> GstRateOptions,
    string HomeStateCode,
    bool RoundOffEnabled,
    string? DefaultTermsAndConditions,
    string? DefaultCustomerNotes,
    decimal DefaultAdvanceMonths,
    IReadOnlyList<QuotationTdsOptionDto> TdsOptions,
    IReadOnlyList<QuotationServiceOptionDto> Services,
    IReadOnlyList<QuotationStateOptionDto> States);

// ── Write models ────────────────────────────────────────────────────────────

public record SaveQuotationLineRequest(
    byte? LineType,
    byte? ServiceType,
    int? WorkspaceTypeId,
    int? ServiceItemId,
    int? WorkspaceId,
    int? PassPlanId,
    string Description,
    string? LineNotes,
    string? HsnSacCode,
    decimal Quantity,
    decimal? DurationValue,
    string? DurationUnit,
    decimal UnitPrice,
    decimal? StandardRate,
    decimal DiscountAmount,
    // Per-line GST rate; falls back to the configured default when absent.
    decimal? TaxPercent);

public record SaveQuotationRequest(
    long CustomerId,
    int LocationId,
    DateOnly? QuotationDate,
    DateOnly? ValidUntil,
    string? ReferenceNumber,
    string? Subject,
    long? SalespersonId,
    string? PlaceOfSupplyStateCode,
    byte? DiscountType,
    decimal? DiscountValue,
    string? TdsCode,
    string? AdjustmentLabel,
    decimal? AdjustmentAmount,
    bool? AdvanceInvoiceEnabled,
    byte? AdvanceType,
    decimal? AdvanceValue,
    string? TermsAndConditions,
    string? Remarks,
    string? InternalNote,
    List<SaveQuotationLineRequest> Lines);

/// <summary>The send popup (document §3.5). At least one channel must be ticked; a
/// send that reaches neither channel leaves the quotation a draft.</summary>
public record SendQuotationRequest(
    bool SendEmail,
    bool SendWhatsApp,
    string? EmailTo,
    string? EmailCc,
    bool BccMe,
    string? EmailSubject,
    string? EmailMessage,
    string? WhatsAppTo,
    string? WhatsAppMessage);

/// <summary>The customer's own decision from the portal (document §3.7). The reason
/// code is required on a rejection; a comment is required when the code is "OTHER".</summary>
public record QuotationDecisionRequest(
    string? ReasonCode,
    string? Reason,
    bool AcceptedTerms);

/// <summary>An admin recording a decision the customer gave by phone or in person
/// (document §3.6). Never the customer's own act, and flagged as such on the record.</summary>
public record RecordQuotationDecisionRequest(
    bool Approved,
    byte Mode,
    string ConfirmedBy,
    string? Note,
    string? ReasonCode);

public record ReviseQuotationRequest(string? Reason);

public record QuotationNoteRequest(string Note);

public record UpdateQuotationAttachmentRequest(bool SendWithEmail);

/// <summary>Body for the cancel route — a reason is mandatory, as on a purchase order.</summary>
public record CancelQuotationRequest(string Reason);

public class QuotationListRequest : PagedRequest
{
    public byte? Status { get; set; }
    public long? CustomerId { get; set; }
    public int? LocationId { get; set; }
    /// <summary>Cabin / Desk / MeetingRoom / Pass / Other — matches any line (§3.1).</summary>
    public byte? ServiceType { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    /// <summary>today | week | month | fy — a shorthand for From/To, which win if both
    /// are given. Anything else is ignored rather than rejected.</summary>
    public string? DatePreset { get; set; }
    /// <summary>The "Expiring in 3 days" card: open quotations lapsing within N days.</summary>
    public int? ExpiringInDays { get; set; }
}

// ── Validation (document §5.4) ──────────────────────────────────────────────

public class SaveQuotationLineRequestValidator : AbstractValidator<SaveQuotationLineRequest>
{
    public SaveQuotationLineRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().WithMessage("Select a service or remove this row.")
            .MaximumLength(300);
        RuleFor(x => x.LineNotes).MaximumLength(500);
        RuleFor(x => x.HsnSacCode).MaximumLength(20);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Must be greater than 0.")
            .When(x => x.LineType != (byte)QuotationLineType.Header);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Rate cannot be negative.");
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxPercent).InclusiveBetween(0, 100).When(x => x.TaxPercent is not null);
        RuleFor(x => x.DurationValue).GreaterThan(0).WithMessage("Must be greater than 0.")
            .When(x => x.DurationValue is not null);
        RuleFor(x => x.DurationUnit).MaximumLength(20);
        RuleFor(x => x.WorkspaceTypeId).GreaterThan(0).When(x => x.WorkspaceTypeId is not null);
        RuleFor(x => x.ServiceItemId).GreaterThan(0).When(x => x.ServiceItemId is not null);
        RuleFor(x => x.WorkspaceId).GreaterThan(0).When(x => x.WorkspaceId is not null);
        RuleFor(x => x.PassPlanId).GreaterThan(0).When(x => x.PassPlanId is not null);
        RuleFor(x => x)
            .Must(x => x.DiscountAmount <= x.Quantity * (x.DurationValue ?? 1m) * x.UnitPrice)
            .WithMessage("Line discount cannot exceed the line value.")
            .WithName(nameof(SaveQuotationLineRequest.DiscountAmount))
            .When(x => x.LineType != (byte)QuotationLineType.Header);
        // A duration without its unit is unreadable on the printed quotation.
        RuleFor(x => x.DurationUnit).NotEmpty()
            .When(x => x.DurationValue is not null)
            .WithMessage("Give the duration a unit, e.g. Hour, Day or Month.");
    }
}

public class SaveQuotationRequestValidator : AbstractValidator<SaveQuotationRequest>
{
    /// <summary>The item table is capped at 100 rows (document §3.3).</summary>
    public const int MaxLines = 100;

    public SaveQuotationRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("Select a customer.");
        RuleFor(x => x.LocationId).GreaterThan(0);
        RuleFor(x => x.ReferenceNumber).MaximumLength(50);
        RuleFor(x => x.Subject).MaximumLength(250);
        RuleFor(x => x.PlaceOfSupplyStateCode).Length(2)
            .When(x => !string.IsNullOrWhiteSpace(x.PlaceOfSupplyStateCode));
        RuleFor(x => x.TermsAndConditions).MaximumLength(2000);
        RuleFor(x => x.Remarks).MaximumLength(1000);
        RuleFor(x => x.InternalNote).MaximumLength(1000);
        RuleFor(x => x.AdjustmentLabel).MaximumLength(50);
        RuleFor(x => x.TdsCode).MaximumLength(20);
        RuleFor(x => x.DiscountValue).GreaterThanOrEqualTo(0)
            .When(x => x.DiscountValue is not null);
        RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100)
            .When(x => x.DiscountType == (byte)QuotationDiscountType.Percentage
                       && x.DiscountValue is not null)
            .WithMessage("A percentage discount cannot be more than 100%.");
        RuleFor(x => x.AdvanceValue).GreaterThanOrEqualTo(0)
            .When(x => x.AdvanceValue is not null);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Add at least one service.");
        RuleFor(x => x.Lines).Must(lines => lines.Count <= MaxLines)
            .WithMessage($"A quotation carries at most {MaxLines} lines.");
        RuleForEach(x => x.Lines).SetValidator(new SaveQuotationLineRequestValidator());
        RuleFor(x => x)
            .Must(x => x.ValidUntil is null || x.QuotationDate is null || x.ValidUntil >= x.QuotationDate)
            .WithMessage("Valid-until date must be on or after the quote date.")
            .WithName(nameof(SaveQuotationRequest.ValidUntil));
    }
}

public class SendQuotationRequestValidator : AbstractValidator<SendQuotationRequest>
{
    public SendQuotationRequestValidator()
    {
        RuleFor(x => x).Must(x => x.SendEmail || x.SendWhatsApp)
            .WithMessage("Pick at least one channel — Email or WhatsApp.")
            .WithName(nameof(SendQuotationRequest.SendEmail));
        RuleFor(x => x.EmailTo).NotEmpty().EmailAddress()
            .WithMessage("Enter a valid email address.")
            .When(x => x.SendEmail);
        RuleFor(x => x.EmailCc).EmailAddress().WithMessage("Enter a valid email address.")
            .When(x => x.SendEmail && !string.IsNullOrWhiteSpace(x.EmailCc));
        RuleFor(x => x.EmailSubject).MaximumLength(250);
        RuleFor(x => x.EmailMessage).MaximumLength(4000);
        RuleFor(x => x.WhatsAppTo).NotEmpty()
            .Matches(@"^\+?[0-9][0-9\s-]{7,19}$")
            .WithMessage("Enter a valid WhatsApp number.")
            .When(x => x.SendWhatsApp);
        RuleFor(x => x.WhatsAppMessage).MaximumLength(1500);
    }
}

public class QuotationDecisionRequestValidator : AbstractValidator<QuotationDecisionRequest>
{
    public QuotationDecisionRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.ReasonCode).MaximumLength(40);
    }
}

public class RecordQuotationDecisionRequestValidator
    : AbstractValidator<RecordQuotationDecisionRequest>
{
    public RecordQuotationDecisionRequestValidator()
    {
        RuleFor(x => x.ConfirmedBy).NotEmpty().WithMessage("Say who confirmed it.")
            .MaximumLength(150);
        RuleFor(x => x.Mode).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.ReasonCode).MaximumLength(40);
    }
}

public class ReviseQuotationRequestValidator : AbstractValidator<ReviseQuotationRequest>
{
    public ReviseQuotationRequestValidator() => RuleFor(x => x.Reason).MaximumLength(500);
}

public class QuotationNoteRequestValidator : AbstractValidator<QuotationNoteRequest>
{
    public QuotationNoteRequestValidator() =>
        RuleFor(x => x.Note).NotEmpty().MaximumLength(1000);
}

public class CancelQuotationRequestValidator : AbstractValidator<CancelQuotationRequest>
{
    public CancelQuotationRequestValidator() =>
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Give a reason for the cancellation.")
            .MaximumLength(500);
}

// ── Service seam ────────────────────────────────────────────────────────────

/// <summary>
/// Quotations (document §6–§8). A quotation is freely editable while Draft; sending it
/// snapshots the customer, company and tax details, and from that point the figures the
/// customer saw are the figures that stand. Approval and rejection are the customer's
/// own act, recorded with their identity and the moment they made it.
///
/// New prices go out as a revision rather than an edit: <see cref="ReviseAsync"/> opens
/// a fresh draft numbered …-R1 and marks the old one Revised, so the document the
/// customer approves is always the document they were sent (document §1).
/// </summary>
public interface IQuotationService
{
    Task<PagedResult<QuotationDto>> GetQuotationsAsync(QuotationListRequest request,
        CancellationToken ct = default);

    /// <summary>The four summary cards and the status tab counts (document §3.1).</summary>
    Task<QuotationSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    /// <summary>Everything the create form needs, in one call (document §3.2).</summary>
    Task<QuotationFormOptionsDto> GetFormOptionsAsync(CancellationToken ct = default);

    /// <summary>The filtered list as CSV, for the Export button (document §3.1).</summary>
    Task<byte[]> ExportAsync(QuotationListRequest request, CancellationToken ct = default);

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

    /// <summary>Draft only. The number is not reused (document §5.3).</summary>
    Task DeleteAsync(long quotationId, CancellationToken ct = default);

    /// <summary>Draft to Sent: snapshots customer, company, place of supply and the
    /// tax rates, freezes the PDF, and delivers on the chosen channels.</summary>
    Task<QuotationDto> SendAsync(long quotationId, SendQuotationRequest request,
        CancellationToken ct = default);

    /// <summary>Opens a new draft …-R1 and marks this one Revised (document §1).</summary>
    Task<QuotationDto> ReviseAsync(long quotationId, ReviseQuotationRequest request,
        CancellationToken ct = default);

    /// <summary>Copies the quotation into a brand-new draft with its own number.</summary>
    Task<QuotationDto> CloneAsync(long quotationId, CancellationToken ct = default);

    Task<QuotationDto> ApproveAsync(long quotationId, QuotationDecisionRequest request,
        CancellationToken ct = default);

    Task<QuotationDto> RejectAsync(long quotationId, QuotationDecisionRequest request,
        CancellationToken ct = default);

    /// <summary>An admin recording the customer's decision from a call or a visit.</summary>
    Task<QuotationDto> RecordDecisionAsync(long quotationId,
        RecordQuotationDecisionRequest request, CancellationToken ct = default);

    Task<QuotationDto> CancelAsync(long quotationId, string reason, CancellationToken ct = default);

    /// <summary>Admin-only note, kept off the customer's copy and off the PDF.</summary>
    Task<QuotationDto> AddInternalNoteAsync(long quotationId, string note,
        CancellationToken ct = default);

    Task<QuotationDto> AddAttachmentAsync(long quotationId, AttachmentUpload upload,
        CancellationToken ct = default);

    Task<QuotationDto> UpdateAttachmentAsync(long quotationId, long attachmentId,
        bool sendWithEmail, CancellationToken ct = default);

    Task<QuotationDto> RemoveAttachmentAsync(long quotationId, long attachmentId,
        CancellationToken ct = default);

    Task<(byte[] Content, string ContentType, string FileName)> GetAttachmentAsync(
        long quotationId, long attachmentId, CancellationToken ct = default);

    /// <summary>The quotation as a PDF, generated from the snapshot.</summary>
    Task<byte[]> GetQuotationPdfAsync(long quotationId, CancellationToken ct = default);
}
