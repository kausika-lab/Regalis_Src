using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Regalis.Application.Finance.Billing;

// ── TDS (build doc §10) ─────────────────────────────────────────────────────────

public record TdsSummaryDto(string FinancialYear, byte Quarter, decimal DeductedThisQuarter, int PaymentsThisQuarter, decimal NotYetPaid,
    int NotYetPaidRows, DateOnly? ChallanDue, decimal ReceivableThisYear, int ReceivableCustomers, int CertificatesMissing,
    decimal CertificatesMissingValue, string NextReturn, DateOnly NextReturnDue);

public record TdsEntryDto(long TdsEntryId, byte Direction, DateOnly DeductedOn, long PartyId, string PartyName, string? PartyTaxId, string Section,
    decimal BaseAmount, decimal Rate, decimal TdsAmount, byte Quarter, string FinancialYear, string? DocumentNumber, string SourceType, long SourceId,
    long? TdsChallanId, string? ChallanNo, string? CertificateNo, DateOnly? CertificateReceivedOn, bool HasCertificateFile, bool CertificateOverdue);

public record TdsChallanDto(long TdsChallanId, string BsrCode, string ChallanNo, DateOnly PaidOn, decimal Amount, byte Quarter, string FinancialYear,
    int Deductions, decimal Covered);

public record RecordChallanRequest(string BsrCode, string ChallanNo, DateOnly PaidOn, decimal Amount, List<long> TdsEntryIds);

public record TdsCertificateRequest(string CertificateNo, DateOnly ReceivedOn);

public record TdsSectionRowDto(int TdsSectionId, string Section, string Description, decimal Rate, decimal? IndividualRate, decimal NoPanRate,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive);

public record SaveTdsRateRequest(string Section, string Description, decimal Rate, decimal? IndividualRate, decimal NoPanRate, DateOnly EffectiveFrom);

public interface ITdsService
{
    Task<TdsSummaryDto> GetSummaryAsync(string? financialYear, byte? quarter, CancellationToken ct = default);
    Task<IReadOnlyList<TdsEntryDto>> GetEntriesAsync(byte direction, string? financialYear, byte? quarter, string? section, bool? open, CancellationToken ct = default);
    Task<IReadOnlyList<TdsChallanDto>> GetChallansAsync(string? financialYear, CancellationToken ct = default);
    Task<TdsChallanDto> RecordChallanAsync(RecordChallanRequest request, CancellationToken ct = default);
    Task<TdsEntryDto> SaveCertificateAsync(long entryId, TdsCertificateRequest request, AttachmentUploadInput? file, CancellationToken ct = default);
    Task<AttachmentFileDto> GetCertificateAsync(long entryId, CancellationToken ct = default);
    Task<IReadOnlyList<TdsSectionRowDto>> GetSectionsAsync(CancellationToken ct = default);
    Task<TdsSectionRowDto> SaveRateAsync(SaveTdsRateRequest request, CancellationToken ct = default);
    Task<byte[]> ExportAsync(byte direction, string? financialYear, byte? quarter, CancellationToken ct = default);
}

// ── GST (doc §11) ────────────────────────────────────────────────────────────────

public record GstTaxTotalsDto(decimal Cgst, decimal Sgst, decimal Igst, decimal Total);

public record GstSummaryDto(string Period, GstTaxTotalsDto Output, GstTaxTotalsDto Input, decimal HeldBack, int HeldBackBills, decimal Claimable,
    decimal CashToPay, decimal CreditCarriedForward, DateOnly PaymentDue, bool Has2b, int InvoiceCount, int BillCount, string? CompanyGstin);

public record GstRateRowDto(decimal Rate, decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst, int Documents);

public record GstSalesRowDto(long InvoiceId, string InvoiceNumber, DateOnly InvoiceDate, string CustomerName, string? CustomerGstin, decimal Rate,
    decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst, decimal Total, byte Status);

public record GstPurchaseRowDto(long PurchaseBillId, string BillNumber, string SupplierBillNo, DateOnly BillDate, string SupplierName, string? SupplierGstin,
    decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst, decimal Total, string? Gstr2bResult);

public record GstRegisterDto(string Period, IReadOnlyList<GstRateRowDto> ByRate, GstRateRowDto Total, IReadOnlyList<GstSalesRowDto> Sales,
    IReadOnlyList<GstPurchaseRowDto> Purchases);

public record Gstr2bRowDto(long Gstr2bLineId, string SupplierGstin, string? SupplierName, string BillNo, DateOnly? BillDate, decimal YourTax,
    decimal Tax2b, byte Result, string ResultLabel, string Action, long? PurchaseBillId);

public record Gstr2bResultDto(string Period, DateTime? UploadedUtc, int Matches, int MissingIn2b, int Differs, int MissingHere, decimal HeldBack,
    decimal UnclaimedCredit, IReadOnlyList<Gstr2bRowDto> Rows);

public record GstReturnRowDto(string Period, string ReturnType, byte Status, string? Arn, DateOnly? FiledOn, string? FiledBy, DateOnly DueDate);

public record SaveGstReturnRequest(string Period, string ReturnType, byte Status, string? Arn, DateOnly? FiledOn);

public record GstRateOptionRowDto(int GstRateId, decimal Rate, string Label, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive);

public record SaveGstRateRequest(decimal Rate, string Label, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive);

public interface IGstService
{
    Task<GstSummaryDto> GetSummaryAsync(string period, CancellationToken ct = default);
    Task<GstRegisterDto> GetSalesRegisterAsync(string period, CancellationToken ct = default);
    Task<GstRegisterDto> GetPurchaseRegisterAsync(string period, CancellationToken ct = default);
    Task<byte[]> ExportRegisterAsync(string kind, string period, CancellationToken ct = default);
    Task<Gstr2bResultDto> Upload2bAsync(string period, BankUploadInput file, CancellationToken ct = default);
    Task<Gstr2bResultDto> Get2bAsync(string period, CancellationToken ct = default);
    Task<IReadOnlyList<GstReturnRowDto>> GetReturnsAsync(string? financialYear, CancellationToken ct = default);
    Task<GstReturnRowDto> SaveReturnAsync(SaveGstReturnRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<GstRateOptionRowDto>> GetRatesAsync(CancellationToken ct = default);
    Task<GstRateOptionRowDto> SaveRateAsync(int? id, SaveGstRateRequest request, CancellationToken ct = default);
}
