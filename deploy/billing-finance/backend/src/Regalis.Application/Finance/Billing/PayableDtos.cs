using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance.Billing;

// ── Purchase bills (build doc §4, §5) ─────────────────────────────────────────

public class PurchaseBillListRequest : PagedRequest
{
    public int? SupplierId { get; set; }
    public byte? Status { get; set; }

    /// <summary>unpaid, due-week, overdue, awaiting.</summary>
    public string? Quick { get; set; }

    /// <summary>2026-27.</summary>
    public string? FinancialYear { get; set; }

    public long? PurchaseOrderId { get; set; }
}

public record PurchaseBillListItemDto(
    long PurchaseBillId, string BillNumber, int SupplierId, string SupplierName, string SupplierBillNo, DateOnly BillDate,
    DateOnly DueDate, long? PurchaseOrderId, string? PoNumber, decimal BillTotal, decimal TdsAmount, decimal Paid, decimal Balance,
    byte Status, bool IsOverdue, int DaysLate, int AttachmentCount, bool CanEdit, bool CanPay);

public record PurchaseBillListResultDto(PagedResult<PurchaseBillListItemDto> Page, decimal FilterBillTotal, decimal FilterPaid, decimal FilterBalance, int FilterCount);

public record PurchaseBillSummaryDto(
    decimal TotalOutstanding, int OutstandingCount, decimal DueThisWeek, int DueThisWeekCount, decimal Overdue, int OverdueCount,
    int OldestOverdueDays, int AwaitingCount, decimal AwaitingValue, decimal InputGstThisMonth);

public record SavePurchaseBillLineRequest(string Description, string? HsnSac, decimal Quantity, decimal Rate, decimal GstRate);

public record SavePurchaseBillRequest(
    int SupplierId, long? PurchaseOrderId, string SupplierBillNo, DateOnly BillDate, int PaymentTermsDays, DateOnly DueDate,
    int? LocationId, string? TdsSection, string? Notes, List<SavePurchaseBillLineRequest> Lines, bool Submit, bool AcknowledgeNoGstin);

public record PurchaseBillLineDto(int LineNumber, string Description, string? HsnSac, decimal Quantity, decimal Rate, decimal TaxableValue,
    decimal GstRate, decimal CgstAmount, decimal SgstAmount, decimal IgstAmount);

public record PurchaseBillAttachmentDto(long AttachmentId, string FileName, string ContentType, long SizeBytes, string? UploadedBy, DateTime UploadedUtc);

public record PurchaseBillEventDto(string EventType, string? Comment, string? UserName, DateTime EventUtc);

public record BillPaymentRowDto(long VendorPaymentId, string PaymentNumber, DateOnly PaidOn, string Mode, string? Reference,
    decimal Amount, decimal TdsAmount, bool IsReconciled, bool IsReversed);

public record PurchaseBillDto(
    long PurchaseBillId, string BillNumber, int SupplierId, string SupplierName, string? SupplierGstin, string? SupplierPan,
    string SupplierBillNo, DateOnly BillDate, DateOnly DueDate, int PaymentTermsDays, long? PurchaseOrderId, string? PoNumber,
    int LocationId, string? LocationName, string? SupplierStateCode, string? PlaceOfSupplyCode, string? PlaceOfSupplyName, bool IsInterState,
    string GstReason, decimal TaxableValue, decimal CgstAmount, decimal SgstAmount, decimal IgstAmount, decimal BillTotal,
    string? TdsSection, decimal TdsRate, decimal TdsAmount, decimal PayableToSupplier, decimal Paid, decimal TdsDeducted, decimal Balance,
    decimal StillToPay, byte Status, bool IsOverdue, int DaysLate, string? EnteredBy, long EnteredByUserId, DateTime CreatedDate,
    DateTime? SubmittedUtc, string? ApprovedBy, DateTime? ApprovedUtc, string? ApprovalComment, string? RejectReason, string? HoldReason,
    string? CancelReason, string? Notes, decimal ApprovalLimit, IReadOnlyList<PurchaseBillLineDto> Lines,
    IReadOnlyList<BillPaymentRowDto> Payments, IReadOnlyList<PurchaseBillAttachmentDto> Attachments, IReadOnlyList<PurchaseBillEventDto> History,
    bool CanEdit, bool CanSubmit, bool CanApprove, bool CanPay, bool CanHold, bool CanCancel);

public record BillDecisionRequest(string? Comment);

public record TdsSectionOptionDto(string Section, string Description, decimal Rate, decimal? IndividualRate, decimal NoPanRate);

public record SupplierOptionDto(int SupplierId, string SupplierName, string SupplierCode, string? Gstin, string? Pan, string? StateCode,
    string? DefaultTdsSection, int? PaymentTermsDays, bool IsIndividual, bool IsActive);

public record PurchaseBillFormOptionsDto(
    IReadOnlyList<SupplierOptionDto> Suppliers, IReadOnlyList<LocationOptionDto> Locations, IReadOnlyList<TdsSectionOptionDto> TdsSections,
    IReadOnlyList<decimal> GstRates, int DefaultTermsDays, string HomeStateCode, decimal ApprovalLimit, string NextBillNumber);

public record LocationOptionDto(int LocationId, string LocationName, string? StateCode);

public record PoBillPrefillDto(long PurchaseOrderId, string PoNumber, int SupplierId, int LocationId, string? TdsSection,
    IReadOnlyList<SavePurchaseBillLineRequest> Lines, IReadOnlyList<string> ExistingBills);

public record AttachmentUploadInput(string FileName, string ContentType, long Length, Stream Content);

public record AttachmentFileDto(byte[] Content, string ContentType, string FileName);

public interface IPurchaseBillService
{
    Task<PurchaseBillListResultDto> GetBillsAsync(PurchaseBillListRequest request, CancellationToken ct = default);
    Task<PurchaseBillSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<PurchaseBillDto> GetBillAsync(long id, CancellationToken ct = default);
    Task<PurchaseBillFormOptionsDto> GetFormOptionsAsync(CancellationToken ct = default);
    Task<PoBillPrefillDto> GetPoPrefillAsync(long purchaseOrderId, CancellationToken ct = default);
    Task<PurchaseBillDto> CreateAsync(SavePurchaseBillRequest request, CancellationToken ct = default);
    Task<PurchaseBillDto> UpdateAsync(long id, SavePurchaseBillRequest request, CancellationToken ct = default);
    Task<PurchaseBillDto> SubmitAsync(long id, CancellationToken ct = default);
    Task<PurchaseBillDto> ApproveAsync(long id, BillDecisionRequest request, CancellationToken ct = default);
    Task<PurchaseBillDto> RejectAsync(long id, BillDecisionRequest request, CancellationToken ct = default);
    Task<PurchaseBillDto> HoldAsync(long id, BillDecisionRequest request, CancellationToken ct = default);
    Task<PurchaseBillDto> ReleaseAsync(long id, CancellationToken ct = default);
    Task<PurchaseBillDto> CancelAsync(long id, BillDecisionRequest request, CancellationToken ct = default);
    Task<PurchaseBillDto> AddAttachmentAsync(long id, AttachmentUploadInput file, CancellationToken ct = default);
    Task<AttachmentFileDto> GetAttachmentAsync(long id, long attachmentId, CancellationToken ct = default);
    Task<byte[]> ExportAsync(PurchaseBillListRequest request, CancellationToken ct = default);
    Task<SupplierOptionDto> GetSupplierProfileAsync(int supplierId, CancellationToken ct = default);
    Task<SupplierOptionDto> SaveSupplierProfileAsync(int supplierId, SaveSupplierTaxProfileRequest request, CancellationToken ct = default);
}

// ── Vendor payments (doc §6) ───────────────────────────────────────────────────

public record OutstandingBillDto(long PurchaseBillId, string BillNumber, string SupplierBillNo, DateOnly BillDate, string? PoNumber,
    DateOnly DueDate, bool IsOverdue, decimal BillTotal, decimal Balance, decimal TdsOnBalance, string? TdsSection, decimal TdsRate, byte Status);

public record SupplierPayablesDto(int SupplierId, string SupplierName, string? Pan, decimal AdvanceAvailable,
    IReadOnlyList<OutstandingBillDto> Bills, IReadOnlyList<OutstandingBillDto> NotPayable);

public record VendorAllocationRequest(long PurchaseBillId, decimal Amount, decimal TdsAmount);

/// <summary>Amount is what leaves the bank: the allocations less the advance adjusted, or the
/// advance itself when no bill is ticked. The server checks it to the rupee.</summary>
public record CreateVendorPaymentRequest(int SupplierId, DateOnly PaidOn, int? BankAccountId, bool FromPettyCash, byte Mode,
    string? Reference, string? Note, decimal Amount, decimal AdvanceToAdjust, List<VendorAllocationRequest> Allocations);

public class VendorPaymentListRequest : PagedRequest
{
    public int? SupplierId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public bool? Reconciled { get; set; }
}

public record VendorPaymentAllocationDto(long PurchaseBillId, string BillNumber, string SupplierBillNo, decimal Amount, decimal TdsAmount);

public record VendorPaymentDto(long VendorPaymentId, string PaymentNumber, int SupplierId, string SupplierName, DateOnly PaidOn,
    decimal Amount, decimal TdsAmount, decimal AdvanceAdjusted, decimal AdvanceAmount, byte Mode, string ModeName, string? Reference,
    int? BankAccountId, string? PaidFrom, bool FromPettyCash, string? Note, int BillCount, bool IsReconciled, bool IsReversed,
    string? ReverseReason, string? PaidBy, DateTime CreatedDate, IReadOnlyList<VendorPaymentAllocationDto> Allocations, bool CanReverse);

public record VendorPaymentResultDto(VendorPaymentDto Payment, string Message);

public interface IVendorPaymentService
{
    Task<SupplierPayablesDto> GetSupplierPayablesAsync(int supplierId, CancellationToken ct = default);
    Task<PagedResult<VendorPaymentDto>> GetPaymentsAsync(VendorPaymentListRequest request, CancellationToken ct = default);
    Task<VendorPaymentDto> GetPaymentAsync(long id, CancellationToken ct = default);
    Task<VendorPaymentResultDto> CreateAsync(CreateVendorPaymentRequest request, CancellationToken ct = default);
    Task<VendorPaymentDto> ReverseAsync(long id, ReversePaymentRequest request, CancellationToken ct = default);
}

// ── Supplier tax profile (doc §10.7, §19.3) ─────────────────────────────────────

public record SaveSupplierTaxProfileRequest(string? Pan, string? DefaultTdsSection, int? PaymentTermsDays, string? StateCode, bool IsIndividual);
