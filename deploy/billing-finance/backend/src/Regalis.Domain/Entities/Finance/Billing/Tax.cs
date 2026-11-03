using System;
using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Finance.Billing;

public enum TdsDirection : byte
{
    /// <summary>Held back from a supplier; owed to the government.</summary>
    Payable = 1,

    /// <summary>Held back by a customer; claimable against Regalis's own tax.</summary>
    Receivable = 2
}

/// <summary>TRN_TdsEntry - one deduction, written when money moves: a vendor payment with
/// TDS (payable) or a bank match explained as "TDS deducted by the customer" (receivable).
/// Never written when a bill is entered (doc §10.3).</summary>
public class TdsEntry : AuditableEntity
{
    public long TdsEntryId { get; set; }
    public TdsDirection Direction { get; set; }

    /// <summary>"Supplier" (SupplierId) or "Customer" (UserId).</summary>
    public string PartyType { get; set; } = string.Empty;
    public long PartyId { get; set; }
    public string PartyName { get; set; } = string.Empty;

    /// <summary>Supplier PAN or customer TAN at the time.</summary>
    public string? PartyTaxId { get; set; }

    public string Section { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal TdsAmount { get; set; }
    public DateOnly DeductedOn { get; set; }

    /// <summary>Q1..Q4 of the Indian financial year (Apr-Jun = Q1).</summary>
    public byte Quarter { get; set; }

    /// <summary>2026-27.</summary>
    public string FinancialYear { get; set; } = string.Empty;

    /// <summary>"VendorPayment" or "BankMatch".</summary>
    public string SourceType { get; set; } = string.Empty;
    public long SourceId { get; set; }
    public string? DocumentNumber { get; set; }

    public long? TdsChallanId { get; set; }

    public string? CertificateNo { get; set; }
    public DateOnly? CertificateReceivedOn { get; set; }
    public string? CertificatePath { get; set; }
    public string? CertificateFileName { get; set; }
}

/// <summary>TRN_TdsChallan - one payment of TDS to the government, covering many deductions.</summary>
public class TdsChallan : AuditableEntity
{
    public long TdsChallanId { get; set; }
    public string BsrCode { get; set; } = string.Empty;
    public string ChallanNo { get; set; } = string.Empty;
    public DateOnly PaidOn { get; set; }
    public decimal Amount { get; set; }
    public byte Quarter { get; set; }
    public string FinancialYear { get; set; } = string.Empty;
}

/// <summary>MST_TdsSection - rates with an effective-date history: a rate change adds a row,
/// so last year's deductions keep the rate they were made at.</summary>
public class TdsSection
{
    public int TdsSectionId { get; set; }
    public string Section { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Rate { get; set; }

    /// <summary>Rate for an individual or HUF where it differs (194C 1%).</summary>
    public decimal? IndividualRate { get; set; }

    public decimal NoPanRate { get; set; } = 20m;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>MST_GstRate - the GST slabs with effective dates.</summary>
public class GstRate
{
    public int GstRateId { get; set; }
    public decimal Rate { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum GstReturnStatus : byte
{
    NotStarted = 1,
    Ready = 2,
    Filed = 3
}

/// <summary>TRN_GstReturn - the return tracker: GSTR-1 and GSTR-3B per month. Nothing is filed
/// from here; it answers "did we file September?".</summary>
public class GstReturn : AuditableEntity
{
    public long GstReturnId { get; set; }

    /// <summary>yyyy-MM.</summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>GSTR1 or GSTR3B.</summary>
    public string ReturnType { get; set; } = string.Empty;

    public GstReturnStatus Status { get; set; } = GstReturnStatus.NotStarted;
    public string? Arn { get; set; }
    public DateOnly? FiledOn { get; set; }
    public long? FiledByUserId { get; set; }
}

public enum Gstr2bResult : byte
{
    Matches = 1,
    MissingIn2b = 2,
    Differs = 3,
    MissingHere = 4
}

/// <summary>TRN_Gstr2bLine - one line of an uploaded GSTR-2B, and how it compared.</summary>
public class Gstr2bLine
{
    public long Gstr2bLineId { get; set; }
    public string Period { get; set; } = string.Empty;
    public string SupplierGstin { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string BillNo { get; set; } = string.Empty;
    public DateOnly? BillDate { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
    public decimal Igst { get; set; }
    public long? MatchedBillId { get; set; }
    public Gstr2bResult Result { get; set; }
    public DateTime UploadedUtc { get; set; }
    public long UploadedByUserId { get; set; }
}
