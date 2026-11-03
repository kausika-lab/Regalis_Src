using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Masters;

namespace Regalis.Infrastructure.Persistence.Configurations;

// Billing & Finance build (Nov 2026). Rules that must hold even when two people save at
// once live here as keys and check constraints, not only in the services (doc §15.3).

public class InvoicePaymentConfiguration : IEntityTypeConfiguration<InvoicePayment>
{
    public void Configure(EntityTypeBuilder<InvoicePayment> builder)
    {
        builder.ToTable("TRN_InvoicePayment", t =>
        {
            t.HasCheckConstraint("CK_InvoicePayment_Amount", "[Amount] <> 0");
            t.HasCheckConstraint("CK_InvoicePayment_Mode", "[Mode] BETWEEN 1 AND 8");
            t.HasCheckConstraint("CK_InvoicePayment_Reference", "[Mode] IN (1,6,7,8) OR [ReversalOfId] IS NOT NULL OR [Reference] IS NOT NULL");
            t.HasCheckConstraint("CK_InvoicePayment_Reversal", "([ReversalOfId] IS NULL AND [Amount] > 0) OR ([ReversalOfId] IS NOT NULL AND [Amount] < 0)");
        });
        builder.HasKey(x => x.InvoicePaymentId);
        builder.Property(x => x.ReceiptNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.Property(x => x.Note).HasMaxLength(300);
        builder.Property(x => x.ReverseReason).HasMaxLength(300);
        builder.HasIndex(x => x.ReceiptNumber).IsUnique().HasDatabaseName("UX_InvoicePayment_Receipt");
        builder.HasIndex(x => x.InvoiceId).HasDatabaseName("IX_InvoicePayment_Invoice");
        builder.HasIndex(x => new { x.CustomerId, x.PaidOn }).HasDatabaseName("IX_InvoicePayment_Customer_Date");
        builder.HasIndex(x => x.PaymentId).HasDatabaseName("IX_InvoicePayment_Payment");
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankAccount>().WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InvoicePayment>().WithMany().HasForeignKey(x => x.ReversalOfId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CustomerTaxProfileConfiguration : IEntityTypeConfiguration<CustomerTaxProfile>
{
    public void Configure(EntityTypeBuilder<CustomerTaxProfile> builder)
    {
        builder.ToTable("MST_CustomerTaxProfile", t =>
            t.HasCheckConstraint("CK_CustomerTaxProfile_Tan", "[Tan] IS NULL OR [Tan] LIKE '[A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9][0-9][A-Z]'"));
        builder.HasKey(x => x.CustomerId);
        builder.Property(x => x.CustomerId).ValueGeneratedNever();
        builder.Property(x => x.Tan).HasMaxLength(10);
        builder.Property(x => x.DoNotChaseReason).HasMaxLength(300);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupplierTaxProfileConfiguration : IEntityTypeConfiguration<SupplierTaxProfile>
{
    public void Configure(EntityTypeBuilder<SupplierTaxProfile> builder)
    {
        builder.ToTable("MST_SupplierTaxProfile", t =>
            t.HasCheckConstraint("CK_SupplierTaxProfile_Pan", "[Pan] IS NULL OR [Pan] LIKE '[A-Z][A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9][A-Z]'"));
        builder.HasKey(x => x.SupplierId);
        builder.Property(x => x.SupplierId).ValueGeneratedNever();
        builder.Property(x => x.Pan).HasMaxLength(10);
        builder.Property(x => x.DefaultTdsSection).HasMaxLength(10);
        builder.Property(x => x.StateCode).HasMaxLength(2);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseBillConfiguration : IEntityTypeConfiguration<PurchaseBill>
{
    public void Configure(EntityTypeBuilder<PurchaseBill> builder)
    {
        builder.ToTable("TRN_PurchaseBill", t =>
        {
            t.HasCheckConstraint("CK_PurchaseBill_Dates", "[DueDate] >= [BillDate]");
            t.HasCheckConstraint("CK_PurchaseBill_Total", "[BillTotal] >= 0 AND [TaxableValue] >= 0 AND [TdsAmount] >= 0");
            t.HasCheckConstraint("CK_PurchaseBill_Status", "[Status] BETWEEN 1 AND 8");
        });
        builder.HasKey(x => x.PurchaseBillId);
        builder.Property(x => x.BillNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.SupplierBillNo).HasMaxLength(60).IsRequired();
        builder.Property(x => x.SupplierBillNoKey).HasMaxLength(60).IsRequired();
        builder.Property(x => x.SupplierStateCode).HasMaxLength(2);
        builder.Property(x => x.PlaceOfSupplyCode).HasMaxLength(2);
        builder.Property(x => x.SupplierGstin).HasMaxLength(15);
        builder.Property(x => x.TdsSection).HasMaxLength(10);
        builder.Property(x => x.TdsRate).HasPrecision(5, 2);
        builder.Property(x => x.ApprovalComment).HasMaxLength(500);
        builder.Property(x => x.RejectReason).HasMaxLength(500);
        builder.Property(x => x.HoldReason).HasMaxLength(500);
        builder.Property(x => x.CancelReason).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.BillNumber).IsUnique().HasDatabaseName("UX_PurchaseBill_Number");
        // Cancelled bills keep their number on record but free it for a correct re-entry.
        builder.HasIndex(x => new { x.SupplierId, x.SupplierBillNoKey }).IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [Status] <> 8").HasDatabaseName("UX_PurchaseBill_Supplier_BillNo");
        builder.HasIndex(x => new { x.Status, x.DueDate }).HasDatabaseName("IX_PurchaseBill_Status_Due");
        builder.HasIndex(x => x.BillDate).HasDatabaseName("IX_PurchaseBill_BillDate");
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.PurchaseBillId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.PurchaseBillId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.PurchaseBillId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseBillLineConfiguration : IEntityTypeConfiguration<PurchaseBillLine>
{
    public void Configure(EntityTypeBuilder<PurchaseBillLine> builder)
    {
        builder.ToTable("TRN_PurchaseBillLine", t =>
            t.HasCheckConstraint("CK_PurchaseBillLine_Qty", "[Quantity] > 0 AND [Rate] >= 0"));
        builder.HasKey(x => x.PurchaseBillLineId);
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
        builder.Property(x => x.HsnSac).HasMaxLength(10);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.GstRate).HasPrecision(5, 2);
    }
}

public class PurchaseBillAttachmentConfiguration : IEntityTypeConfiguration<PurchaseBillAttachment>
{
    public void Configure(EntityTypeBuilder<PurchaseBillAttachment> builder)
    {
        builder.ToTable("TRN_PurchaseBillAttachment");
        builder.HasKey(x => x.PurchaseBillAttachmentId);
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.StoredPath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
    }
}

public class PurchaseBillEventConfiguration : IEntityTypeConfiguration<PurchaseBillEvent>
{
    public void Configure(EntityTypeBuilder<PurchaseBillEvent> builder)
    {
        builder.ToTable("TRN_PurchaseBillEvent");
        builder.HasKey(x => x.PurchaseBillEventId);
        builder.Property(x => x.EventType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(500);
    }
}

public class VendorPaymentConfiguration : IEntityTypeConfiguration<VendorPayment>
{
    public void Configure(EntityTypeBuilder<VendorPayment> builder)
    {
        builder.ToTable("TRN_VendorPayment", t =>
        {
            t.HasCheckConstraint("CK_VendorPayment_Amounts", "[Amount] >= 0 AND [TdsAmount] >= 0 AND [AdvanceAdjusted] >= 0 AND [AdvanceAmount] >= 0 AND [AdvanceAmount] <= [Amount]");
            t.HasCheckConstraint("CK_VendorPayment_Reference", "[Mode] = 6 OR [Reference] IS NOT NULL");
            t.HasCheckConstraint("CK_VendorPayment_Source", "([FromPettyCash] = 1 AND [BankAccountId] IS NULL) OR ([FromPettyCash] = 0 AND [BankAccountId] IS NOT NULL)");
        });
        builder.HasKey(x => x.VendorPaymentId);
        builder.Property(x => x.PaymentNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.Property(x => x.Note).HasMaxLength(300);
        builder.Property(x => x.ReverseReason).HasMaxLength(300);
        builder.HasIndex(x => x.PaymentNumber).IsUnique().HasDatabaseName("UX_VendorPayment_Number");
        builder.HasIndex(x => new { x.SupplierId, x.PaidOn }).HasDatabaseName("IX_VendorPayment_Supplier_Date");
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankAccount>().WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Allocations).WithOne().HasForeignKey(x => x.VendorPaymentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class VendorPaymentAllocationConfiguration : IEntityTypeConfiguration<VendorPaymentAllocation>
{
    public void Configure(EntityTypeBuilder<VendorPaymentAllocation> builder)
    {
        builder.ToTable("TRN_VendorPaymentAllocation", t =>
            t.HasCheckConstraint("CK_VendorPaymentAllocation_Amount", "[Amount] > 0 AND [TdsAmount] >= 0 AND [TdsAmount] <= [Amount]"));
        builder.HasKey(x => x.VendorPaymentAllocationId);
        builder.HasIndex(x => new { x.VendorPaymentId, x.PurchaseBillId }).IsUnique().HasDatabaseName("UX_VendorPaymentAllocation_Payment_Bill");
        builder.HasIndex(x => x.PurchaseBillId).HasDatabaseName("IX_VendorPaymentAllocation_Bill");
        builder.HasOne<PurchaseBill>().WithMany().HasForeignKey(x => x.PurchaseBillId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BankImportBatchConfiguration : IEntityTypeConfiguration<BankImportBatch>
{
    public void Configure(EntityTypeBuilder<BankImportBatch> builder)
    {
        builder.ToTable("TRN_BankImportBatch");
        builder.HasKey(x => x.BankImportBatchId);
        builder.Property(x => x.BatchCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ColumnMappingJson).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => x.BatchCode).IsUnique().HasDatabaseName("UX_BankImportBatch_Code");
        builder.HasOne<BankAccount>().WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BankColumnMappingConfiguration : IEntityTypeConfiguration<BankColumnMapping>
{
    public void Configure(EntityTypeBuilder<BankColumnMapping> builder)
    {
        builder.ToTable("MST_BankColumnMapping");
        builder.HasKey(x => x.BankAccountId);
        builder.Property(x => x.BankAccountId).ValueGeneratedNever();
        builder.Property(x => x.MappingJson).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.DateFormat).HasMaxLength(20).IsRequired();
        builder.HasOne<BankAccount>().WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BankMatchAllocationConfiguration : IEntityTypeConfiguration<BankMatchAllocation>
{
    public void Configure(EntityTypeBuilder<BankMatchAllocation> builder)
    {
        builder.ToTable("TRN_BankMatchAllocation", t =>
            t.HasCheckConstraint("CK_BankMatchAllocation_Amount", "[Amount] > 0"));
        builder.HasKey(x => x.BankMatchAllocationId);
        builder.Property(x => x.DocNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.CreatedRefs).HasMaxLength(500);
        builder.Property(x => x.UnmatchReason).HasMaxLength(300);
        builder.HasIndex(x => x.BankTransactionId).HasDatabaseName("IX_BankMatchAllocation_Line");
        builder.HasIndex(x => new { x.DocType, x.DocId }).HasDatabaseName("IX_BankMatchAllocation_Doc");
        builder.HasOne<BankTransaction>().WithMany().HasForeignKey(x => x.BankTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BankMatchDifferenceConfiguration : IEntityTypeConfiguration<BankMatchDifference>
{
    public void Configure(EntityTypeBuilder<BankMatchDifference> builder)
    {
        builder.ToTable("TRN_BankMatchDifference", t =>
            t.HasCheckConstraint("CK_BankMatchDifference_Amount", "[SignedAmount] <> 0"));
        builder.HasKey(x => x.BankMatchDifferenceId);
        builder.Property(x => x.TdsSection).HasMaxLength(10);
        builder.Property(x => x.Note).HasMaxLength(300);
        builder.HasIndex(x => x.BankTransactionId).HasDatabaseName("IX_BankMatchDifference_Line");
        builder.HasOne<BankTransaction>().WithMany().HasForeignKey(x => x.BankTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DifferenceReasonConfiguration : IEntityTypeConfiguration<DifferenceReason>
{
    public void Configure(EntityTypeBuilder<DifferenceReason> builder)
    {
        builder.ToTable("MST_DifferenceReason");
        builder.HasKey(x => x.DifferenceReasonId);
        builder.Property(x => x.DifferenceReasonId).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Help).HasMaxLength(300);
        builder.Property(x => x.RequiredPermission).HasMaxLength(100);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_DifferenceReason_Code");
    }
}

public class TdsEntryConfiguration : IEntityTypeConfiguration<TdsEntry>
{
    public void Configure(EntityTypeBuilder<TdsEntry> builder)
    {
        builder.ToTable("TRN_TdsEntry", t =>
        {
            t.HasCheckConstraint("CK_TdsEntry_Amount", "[TdsAmount] > 0 AND [BaseAmount] >= 0");
            t.HasCheckConstraint("CK_TdsEntry_Quarter", "[Quarter] BETWEEN 1 AND 4");
        });
        builder.HasKey(x => x.TdsEntryId);
        builder.Property(x => x.PartyType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PartyName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PartyTaxId).HasMaxLength(10);
        builder.Property(x => x.Section).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Rate).HasPrecision(5, 2);
        builder.Property(x => x.FinancialYear).HasMaxLength(7).IsRequired();
        builder.Property(x => x.SourceType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.DocumentNumber).HasMaxLength(40);
        builder.Property(x => x.CertificateNo).HasMaxLength(40);
        builder.Property(x => x.CertificatePath).HasMaxLength(500);
        builder.Property(x => x.CertificateFileName).HasMaxLength(260);
        builder.HasIndex(x => new { x.Direction, x.FinancialYear, x.Quarter }).HasDatabaseName("IX_TdsEntry_Direction_Period");
        builder.HasIndex(x => new { x.SourceType, x.SourceId }).HasDatabaseName("IX_TdsEntry_Source");
        builder.HasIndex(x => x.TdsChallanId).HasDatabaseName("IX_TdsEntry_Challan");
        builder.HasOne<TdsChallan>().WithMany().HasForeignKey(x => x.TdsChallanId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TdsChallanConfiguration : IEntityTypeConfiguration<TdsChallan>
{
    public void Configure(EntityTypeBuilder<TdsChallan> builder)
    {
        builder.ToTable("TRN_TdsChallan", t =>
        {
            t.HasCheckConstraint("CK_TdsChallan_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_TdsChallan_Bsr", "LEN([BsrCode]) = 7");
        });
        builder.HasKey(x => x.TdsChallanId);
        builder.Property(x => x.BsrCode).HasMaxLength(7).IsRequired();
        builder.Property(x => x.ChallanNo).HasMaxLength(10).IsRequired();
        builder.Property(x => x.FinancialYear).HasMaxLength(7).IsRequired();
        builder.HasIndex(x => new { x.BsrCode, x.ChallanNo, x.PaidOn }).IsUnique()
            .HasFilter("[IsDeleted] = 0").HasDatabaseName("UX_TdsChallan_Bsr_No_Date");
    }
}

public class TdsSectionConfiguration : IEntityTypeConfiguration<TdsSection>
{
    public void Configure(EntityTypeBuilder<TdsSection> builder)
    {
        builder.ToTable("MST_TdsSection");
        builder.HasKey(x => x.TdsSectionId);
        builder.Property(x => x.Section).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Rate).HasPrecision(5, 2);
        builder.Property(x => x.IndividualRate).HasPrecision(5, 2);
        builder.Property(x => x.NoPanRate).HasPrecision(5, 2);
        builder.HasIndex(x => new { x.Section, x.EffectiveFrom }).IsUnique().HasDatabaseName("UX_TdsSection_Section_From");
    }
}

public class GstRateConfiguration : IEntityTypeConfiguration<GstRate>
{
    public void Configure(EntityTypeBuilder<GstRate> builder)
    {
        builder.ToTable("MST_GstRate");
        builder.HasKey(x => x.GstRateId);
        builder.Property(x => x.Rate).HasPrecision(5, 2);
        builder.Property(x => x.Label).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.Rate, x.EffectiveFrom }).IsUnique().HasDatabaseName("UX_GstRate_Rate_From");
    }
}

public class GstReturnConfiguration : IEntityTypeConfiguration<GstReturn>
{
    public void Configure(EntityTypeBuilder<GstReturn> builder)
    {
        builder.ToTable("TRN_GstReturn", t =>
        {
            t.HasCheckConstraint("CK_GstReturn_Type", "[ReturnType] IN ('GSTR1','GSTR3B')");
            t.HasCheckConstraint("CK_GstReturn_Filed", "[Status] <> 3 OR ([Arn] IS NOT NULL AND [FiledOn] IS NOT NULL)");
        });
        builder.HasKey(x => x.GstReturnId);
        builder.Property(x => x.Period).HasMaxLength(7).IsRequired();
        builder.Property(x => x.ReturnType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Arn).HasMaxLength(20);
        builder.HasIndex(x => new { x.Period, x.ReturnType }).IsUnique().HasDatabaseName("UX_GstReturn_Period_Type");
    }
}

public class Gstr2bLineConfiguration : IEntityTypeConfiguration<Gstr2bLine>
{
    public void Configure(EntityTypeBuilder<Gstr2bLine> builder)
    {
        builder.ToTable("TRN_Gstr2bLine");
        builder.HasKey(x => x.Gstr2bLineId);
        builder.Property(x => x.Period).HasMaxLength(7).IsRequired();
        builder.Property(x => x.SupplierGstin).HasMaxLength(15).IsRequired();
        builder.Property(x => x.SupplierName).HasMaxLength(200);
        builder.Property(x => x.BillNo).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => x.Period).HasDatabaseName("IX_Gstr2bLine_Period");
    }
}

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("MST_Department");
        builder.HasKey(x => x.DepartmentId);
        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UX_Department_Name");
    }
}

public class ExpenseControlConfiguration : IEntityTypeConfiguration<ExpenseControl>
{
    public void Configure(EntityTypeBuilder<ExpenseControl> builder)
    {
        builder.ToTable("TRN_ExpenseControl", t =>
        {
            t.HasCheckConstraint("CK_ExpenseControl_Reimburse", "[IsReimbursement] = 0 OR [ReimburseToUserId] IS NOT NULL");
            t.HasCheckConstraint("CK_ExpenseControl_Decision", "[ApprovalStatus] <> 3 OR [DecisionComment] IS NOT NULL");
        });
        builder.HasKey(x => x.ExpenseId);
        builder.Property(x => x.ExpenseId).ValueGeneratedNever();
        builder.Property(x => x.DecisionComment).HasMaxLength(500);
        builder.Property(x => x.ReimbursedVia).HasMaxLength(20);
        builder.Property(x => x.ReimbursementReference).HasMaxLength(60);
        builder.HasIndex(x => x.ApprovalStatus).HasDatabaseName("IX_ExpenseControl_Status");
        builder.HasOne<Expense>().WithMany().HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ExpenseReceiptConfiguration : IEntityTypeConfiguration<ExpenseReceipt>
{
    public void Configure(EntityTypeBuilder<ExpenseReceipt> builder)
    {
        builder.ToTable("TRN_ExpenseReceipt");
        builder.HasKey(x => x.ExpenseReceiptId);
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.StoredPath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.ExpenseId).HasDatabaseName("IX_ExpenseReceipt_Expense");
        builder.HasOne<Expense>().WithMany().HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RecurringExpenseConfiguration : IEntityTypeConfiguration<RecurringExpense>
{
    public void Configure(EntityTypeBuilder<RecurringExpense> builder)
    {
        builder.ToTable("TRN_RecurringExpense", t =>
        {
            t.HasCheckConstraint("CK_RecurringExpense_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_RecurringExpense_Day", "[DayOfMonth] BETWEEN 1 AND 28");
        });
        builder.HasKey(x => x.RecurringExpenseId);
        builder.Property(x => x.Category).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
    }
}

public class PettyCashEntryConfiguration : IEntityTypeConfiguration<PettyCashEntry>
{
    public void Configure(EntityTypeBuilder<PettyCashEntry> builder)
    {
        builder.ToTable("TRN_PettyCashEntry", t =>
        {
            t.HasCheckConstraint("CK_PettyCash_NotNegative", "[BalanceAfter] >= 0");
            t.HasCheckConstraint("CK_PettyCash_Sign", "([EntryType] = 1 AND [Amount] > 0) OR ([EntryType] = 2 AND [Amount] < 0) OR [EntryType] = 3");
        });
        builder.HasKey(x => x.PettyCashEntryId);
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(300);
        builder.HasIndex(x => x.HappenedOn).HasDatabaseName("IX_PettyCash_Date");
    }
}

public class ComplianceObligationConfiguration : IEntityTypeConfiguration<ComplianceObligation>
{
    public void Configure(EntityTypeBuilder<ComplianceObligation> builder)
    {
        builder.ToTable("MST_ComplianceObligation");
        builder.HasKey(x => x.ComplianceObligationId);
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Frequency).HasMaxLength(20).IsRequired();
        builder.Property(x => x.DueMonths).HasMaxLength(40);
        builder.Property(x => x.AmountSource).HasMaxLength(100);
        builder.Property(x => x.OwnerRole).HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_ComplianceObligation_Code");
    }
}

public class ComplianceStatusConfiguration : IEntityTypeConfiguration<ComplianceStatus>
{
    public void Configure(EntityTypeBuilder<ComplianceStatus> builder)
    {
        builder.ToTable("TRN_ComplianceStatus");
        builder.HasKey(x => x.ComplianceStatusId);
        builder.Property(x => x.Period).HasMaxLength(7).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.HasIndex(x => new { x.ComplianceObligationId, x.Period }).IsUnique().HasDatabaseName("UX_ComplianceStatus_Obligation_Period");
        builder.HasOne<ComplianceObligation>().WithMany().HasForeignKey(x => x.ComplianceObligationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PeriodLockConfiguration : IEntityTypeConfiguration<PeriodLock>
{
    public void Configure(EntityTypeBuilder<PeriodLock> builder)
    {
        builder.ToTable("TRN_PeriodLock", t =>
            t.HasCheckConstraint("CK_PeriodLock_Period", "[Period] LIKE '[12][0-9][0-9][0-9]-[01][0-9]'"));
        builder.HasKey(x => x.PeriodLockId);
        builder.Property(x => x.Period).HasMaxLength(7).IsRequired();
        builder.Property(x => x.UnlockReason).HasMaxLength(300);
        builder.HasIndex(x => x.Period).IsUnique().HasDatabaseName("UX_PeriodLock_Period");
    }
}

public class ReminderLogConfiguration : IEntityTypeConfiguration<ReminderLog>
{
    public void Configure(EntityTypeBuilder<ReminderLog> builder)
    {
        builder.ToTable("TRN_ReminderLog");
        builder.HasKey(x => x.ReminderLogId);
        builder.Property(x => x.ReminderType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.DocumentType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.DocumentNumber).HasMaxLength(40);
        builder.Property(x => x.Recipient).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Channel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(500);
        builder.HasIndex(x => new { x.DocumentType, x.DocumentId }).HasDatabaseName("IX_ReminderLog_Document");
    }
}
