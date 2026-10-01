using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Entities.Operations;

namespace Regalis.Infrastructure.Persistence.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        // The database itself refuses an overdraft.
        builder.ToTable("MST_Wallet", t =>
            t.HasCheckConstraint("CK_Wallet_Balance", "[Balance] >= 0"));
        builder.HasKey(x => x.WalletId);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.CustomerId).IsUnique().HasDatabaseName("UX_Wallet_Customer");

        builder.HasOne(x => x.Customer).WithMany()
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("TRN_WalletTransaction");
        builder.HasKey(x => x.WalletTransactionId);
        builder.Property(x => x.TransactionNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(40);
        builder.Property(x => x.Narration).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => x.TransactionNumber).IsUnique().HasDatabaseName("UX_WalletTxn_Number");
        builder.HasIndex(x => new { x.CustomerId, x.TransactionUtc })
            .HasDatabaseName("IX_WalletTxn_Customer_Date")
            .IsDescending(false, true);
        builder.HasIndex(x => x.Category).HasDatabaseName("IX_WalletTxn_Category");

        builder.HasOne(x => x.Wallet).WithMany(x => x.Transactions)
            .HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("TRN_Payment");
        builder.HasKey(x => x.PaymentId);
        builder.Property(x => x.PaymentNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Gateway).HasMaxLength(40);
        builder.Property(x => x.GatewayOrderId).HasMaxLength(120);
        builder.Property(x => x.GatewayPaymentId).HasMaxLength(120);
        builder.Property(x => x.GatewaySignature).HasMaxLength(256);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.RefundReference).HasMaxLength(120);

        builder.HasIndex(x => x.PaymentNumber).IsUnique().HasDatabaseName("UX_Payment_Number");
        builder.HasIndex(x => new { x.CustomerId, x.CreatedDate }).HasDatabaseName("IX_Payment_Customer_Date");
        builder.HasIndex(x => new { x.Status, x.PaidUtc }).HasDatabaseName("IX_Payment_Status_Date");
        // Makes gateway webhooks idempotent.
        builder.HasIndex(x => x.GatewayPaymentId).IsUnique()
            .HasDatabaseName("UX_Payment_GatewayPaymentId")
            .HasFilter("[GatewayPaymentId] IS NOT NULL");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("TRN_Invoice");
        builder.HasKey(x => x.InvoiceId);
        builder.Property(x => x.InvoiceNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.CgstPercent).HasPrecision(5, 2);
        builder.Property(x => x.SgstPercent).HasPrecision(5, 2);
        builder.Property(x => x.IgstPercent).HasPrecision(5, 2);
        builder.Property(x => x.TdsPercent).HasPrecision(5, 2);
        builder.Property(x => x.TdsLabel).HasMaxLength(80);
        builder.Property(x => x.AdjustmentLabel).HasMaxLength(50);
        builder.Property(x => x.OutstandingAmount)
            .HasComputedColumnSql("[TotalAmount] - [PaidAmount]", stored: true);
        builder.Property(x => x.CustomerName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.CustomerCompanyName).HasMaxLength(200);
        builder.Property(x => x.CustomerEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CustomerMobile).HasMaxLength(20);
        builder.Property(x => x.CustomerGstin).HasMaxLength(20);
        builder.Property(x => x.BillingAddress).HasMaxLength(500);
        builder.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CompanyGstin).HasMaxLength(20);
        builder.Property(x => x.CompanyAddress).HasMaxLength(500);
        builder.Property(x => x.PdfPath).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.TermsAndConditions).HasMaxLength(2000);

        builder.HasIndex(x => x.InvoiceNumber).IsUnique().HasDatabaseName("UX_Invoice_Number");
        builder.HasIndex(x => new { x.CustomerId, x.InvoiceDate }).HasDatabaseName("IX_Invoice_Customer_Date");
        builder.HasIndex(x => new { x.Status, x.InvoiceDate }).HasDatabaseName("IX_Invoice_Status_Date");
        // One invoice per quotation — the database refuses a second conversion of
        // the same approved quotation, so a double-click cannot double-bill.
        builder.HasIndex(x => x.QuotationId).IsUnique()
            .HasDatabaseName("UX_Invoice_Quotation")
            .HasFilter("[QuotationId] IS NOT NULL");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("TRN_InvoiceLine");
        builder.HasKey(x => x.InvoiceLineId);
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
        builder.Property(x => x.HsnSacCode).HasMaxLength(20);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.TaxPercent).HasPrecision(5, 2);

        builder.HasOne(x => x.Invoice).WithMany(x => x.Lines)
            .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CustomerPassConfiguration : IEntityTypeConfiguration<CustomerPass>
{
    public void Configure(EntityTypeBuilder<CustomerPass> builder)
    {
        builder.ToTable("TRN_CustomerPass");
        builder.HasKey(x => x.CustomerPassId);
        builder.Property(x => x.PassNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.PassNumber).IsUnique().HasDatabaseName("UX_CustomerPass_Number");
        builder.HasIndex(x => new { x.CustomerId, x.Status }).HasDatabaseName("IX_CustomerPass_Customer_Status");
        // Drives the expiry job.
        builder.HasIndex(x => new { x.ValidTo, x.Status }).HasDatabaseName("IX_CustomerPass_ValidTo_Status");

        builder.HasOne(x => x.PassPlan).WithMany()
            .HasForeignKey(x => x.PassPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ServiceOrderConfiguration : IEntityTypeConfiguration<ServiceOrder>
{
    public void Configure(EntityTypeBuilder<ServiceOrder> builder)
    {
        builder.ToTable("TRN_ServiceOrder");
        builder.HasKey(x => x.ServiceOrderId);
        builder.Property(x => x.OrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.OrderNumber).IsUnique().HasDatabaseName("UX_ServiceOrder_Number");
        builder.HasIndex(x => new { x.CustomerId, x.OrderUtc }).HasDatabaseName("IX_ServiceOrder_Customer_Date");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ServiceOrderLineConfiguration : IEntityTypeConfiguration<ServiceOrderLine>
{
    public void Configure(EntityTypeBuilder<ServiceOrderLine> builder)
    {
        builder.ToTable("TRN_ServiceOrderLine");
        builder.HasKey(x => x.ServiceOrderLineId);
        builder.Property(x => x.ItemName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);

        builder.HasOne(x => x.ServiceOrder).WithMany(x => x.Lines)
            .HasForeignKey(x => x.ServiceOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ServiceItem).WithMany()
            .HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("MST_Supplier");
        builder.HasKey(x => x.SupplierId);
        builder.Property(x => x.SupplierCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.SupplierName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ContactPerson).HasMaxLength(150);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.MobileNumber).HasMaxLength(20);
        builder.Property(x => x.Gstin).HasMaxLength(20);
        builder.Property(x => x.AddressLine).HasMaxLength(500);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(100);
        builder.Property(x => x.Pincode).HasMaxLength(10);
        builder.Property(x => x.PaymentTerms).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => x.SupplierCode).IsUnique().HasDatabaseName("UX_Supplier_Code");
        builder.HasIndex(x => new { x.LocationId, x.SupplierName }).HasDatabaseName("IX_Supplier_Location_Name");

        builder.HasOne<Location>().WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("TRN_PurchaseOrder");
        builder.HasKey(x => x.PurchaseOrderId);
        builder.Property(x => x.PoNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.AttachmentPath).HasMaxLength(500);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.ReferenceNumber).HasMaxLength(80);
        builder.Property(x => x.ShipmentPreference).HasMaxLength(60);
        builder.Property(x => x.DeliveryAddress).HasMaxLength(600);
        builder.Property(x => x.TdsCode).HasMaxLength(20);
        builder.Property(x => x.TdsLabel).HasMaxLength(100);
        builder.Property(x => x.TdsPercent).HasPrecision(5, 2);
        builder.Property(x => x.AdjustmentLabel).HasMaxLength(60);
        builder.Property(x => x.TermsAndConditions).HasMaxLength(4000);

        builder.HasIndex(x => x.PoNumber).IsUnique().HasDatabaseName("UX_PurchaseOrder_Number");
        builder.HasIndex(x => new { x.SupplierId, x.PoDate }).HasDatabaseName("IX_PurchaseOrder_Supplier_Date");
        builder.HasIndex(x => new { x.LocationId, x.Status, x.PoDate })
            .HasDatabaseName("IX_PurchaseOrder_Location_Status_Date");

        builder.HasOne(x => x.Supplier).WithMany(x => x.PurchaseOrders)
            .HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        // Receiving can never exceed what was ordered, and never runs backwards.
        builder.ToTable("TRN_PurchaseOrderLine", t =>
            t.HasCheckConstraint("CK_PoLine_Received",
                "[ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity]"));
        builder.HasKey(x => x.PurchaseOrderLineId);
        builder.Property(x => x.ItemName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.HsnSacCode).HasMaxLength(20);
        builder.Property(x => x.Unit).HasMaxLength(20);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.ReceivedQuantity).HasPrecision(18, 3);
        builder.Property(x => x.TaxPercent).HasPrecision(5, 2);
        builder.Property(x => x.Remarks).HasMaxLength(500);
        builder.Property(x => x.AccountCategory).HasMaxLength(100);

        builder.HasOne(x => x.PurchaseOrder).WithMany(x => x.Lines)
            .HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<InventoryItem>().WithMany()
            .HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseOrderAttachmentConfiguration : IEntityTypeConfiguration<PurchaseOrderAttachment>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderAttachment> builder)
    {
        builder.ToTable("TRN_PurchaseOrderAttachment");
        builder.HasKey(x => x.PurchaseOrderAttachmentId);
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.StoredFileName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.PurchaseOrderId).HasDatabaseName("IX_PurchaseOrderAttachment_PurchaseOrder");

        builder.HasOne(x => x.PurchaseOrder).WithMany(x => x.Attachments)
            .HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseOrderStatusHistoryConfiguration : IEntityTypeConfiguration<PurchaseOrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderStatusHistory> builder)
    {
        builder.ToTable("TRN_PurchaseOrderStatusHistory");
        builder.HasKey(x => x.PurchaseOrderStatusHistoryId);
        builder.Property(x => x.Action).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => x.PurchaseOrderId).HasDatabaseName("IX_PurchaseOrderStatusHistory_PurchaseOrder");

        builder.HasOne(x => x.PurchaseOrder).WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        // An expense is money out; zero or negative is a data-entry error, not a
        // correction — reverse a wrong expense by cancelling it, not by negating it.
        builder.ToTable("TRN_Expense", t =>
        {
            t.HasCheckConstraint("CK_Expense_Amount", "[Amount] > 0");
            // Adjustments may take an expense to nil (a full refund), never below.
            t.HasCheckConstraint("CK_Expense_NetAmount", "[Amount] + [AdjustmentTotal] >= 0");
        });
        builder.HasKey(x => x.ExpenseId);
        builder.Property(x => x.ExpenseNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(100).IsRequired();
        // Required by the document, so the database enforces it too.
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Remarks).HasMaxLength(500).IsRequired();
        builder.Property(x => x.AttachmentPath).HasMaxLength(500);

        builder.HasIndex(x => x.ExpenseNumber).IsUnique().HasDatabaseName("UX_Expense_Number");
        builder.HasIndex(x => new { x.LocationId, x.ExpenseDate }).HasDatabaseName("IX_Expense_Location_Date");
        builder.HasIndex(x => new { x.Category, x.ExpenseDate }).HasDatabaseName("IX_Expense_Category_Date");

        builder.HasOne<Location>().WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany()
            .HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany()
            .HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> builder)
    {
        builder.ToTable("TRN_Quotation");
        builder.HasKey(x => x.QuotationId);
        builder.Property(x => x.QuotationNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.CgstPercent).HasPrecision(5, 2);
        builder.Property(x => x.SgstPercent).HasPrecision(5, 2);
        builder.Property(x => x.IgstPercent).HasPrecision(5, 2);
        builder.Property(x => x.CustomerName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.CustomerEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CustomerMobile).HasMaxLength(20);
        builder.Property(x => x.CustomerGstin).HasMaxLength(20);
        builder.Property(x => x.BillingAddress).HasMaxLength(500);
        builder.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CompanyGstin).HasMaxLength(20);
        builder.Property(x => x.CompanyAddress).HasMaxLength(500);
        builder.Property(x => x.PlaceOfSupply).HasMaxLength(100);
        builder.Property(x => x.PlaceOfSupplyStateCode).HasMaxLength(2);
        builder.Property(x => x.CustomerCompanyName).HasMaxLength(200);
        builder.Property(x => x.TermsAndConditions).HasMaxLength(2000);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.InternalNote).HasMaxLength(1000);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.Property(x => x.RejectionCode).HasMaxLength(40);
        builder.Property(x => x.PdfPath).HasMaxLength(500);
        builder.Property(x => x.ReferenceNumber).HasMaxLength(50);
        builder.Property(x => x.Subject).HasMaxLength(250);
        builder.Property(x => x.TdsCode).HasMaxLength(20);
        builder.Property(x => x.TdsLabel).HasMaxLength(80);
        builder.Property(x => x.TdsPercent).HasPrecision(5, 2);
        builder.Property(x => x.DiscountValue).HasPrecision(14, 2);
        builder.Property(x => x.AdjustmentLabel).HasMaxLength(50);
        builder.Property(x => x.AdvanceValue).HasPrecision(14, 2);
        builder.Property(x => x.DecisionIpAddress).HasMaxLength(45);
        builder.Property(x => x.DecisionConfirmedBy).HasMaxLength(150);
        builder.Property(x => x.DecisionNote).HasMaxLength(500);

        builder.HasIndex(x => x.QuotationNumber).IsUnique().HasDatabaseName("UX_Quotation_Number");
        builder.HasIndex(x => new { x.CustomerId, x.QuotationDate }).HasDatabaseName("IX_Quotation_Customer_Date");
        builder.HasIndex(x => new { x.LocationId, x.Status, x.QuotationDate })
            .HasDatabaseName("IX_Quotation_Location_Status_Date");
        // Drives the expiry sweeper, the same shape as IX_CustomerPass_ValidTo_Status.
        builder.HasIndex(x => new { x.ValidUntil, x.Status }).HasDatabaseName("IX_Quotation_ValidUntil_Status");
        // A revision thread is read from the newest back to the original.
        builder.HasIndex(x => x.RevisionOfQuotationId).HasDatabaseName("IX_Quotation_RevisionOf")
            .HasFilter("[RevisionOfQuotationId] IS NOT NULL");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        // The invoice points back through UX_Invoice_Quotation; this side stays a
        // plain column so converting never rewrites the quotation's own foreign keys.
        builder.HasOne<Invoice>().WithMany()
            .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QuotationLineConfiguration : IEntityTypeConfiguration<QuotationLine>
{
    public void Configure(EntityTypeBuilder<QuotationLine> builder)
    {
        builder.ToTable("TRN_QuotationLine");
        builder.HasKey(x => x.QuotationLineId);
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
        builder.Property(x => x.HsnSacCode).HasMaxLength(20);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.DurationValue).HasPrecision(18, 3);
        builder.Property(x => x.DurationUnit).HasMaxLength(20);
        builder.Property(x => x.TaxPercent).HasPrecision(5, 2);
        builder.Property(x => x.LineNotes).HasMaxLength(500);
        builder.Property(x => x.StandardRate).HasPrecision(14, 2);
        builder.Property(x => x.Unit).HasMaxLength(20);
        // Snapshot link (no FK): the Recent Transactions lookup reads by item.
        builder.HasIndex(x => x.InventoryItemId).HasDatabaseName("IX_QuotationLine_InventoryItem")
            .HasFilter("[InventoryItemId] IS NOT NULL");

        builder.HasOne(x => x.Quotation).WithMany(x => x.Lines)
            .HasForeignKey(x => x.QuotationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkspaceType>().WithMany()
            .HasForeignKey(x => x.WorkspaceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceItem>().WithMany()
            .HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
        // WorkspaceId and PassPlanId are snapshots of what was quoted, not live links:
        // a cabin retired next month must not orphan a quotation the customer holds.
    }
}

public class QuotationAttachmentConfiguration : IEntityTypeConfiguration<QuotationAttachment>
{
    public void Configure(EntityTypeBuilder<QuotationAttachment> builder)
    {
        builder.ToTable("TRN_QuotationAttachment");
        builder.HasKey(x => x.QuotationAttachmentId);
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.StoredFileName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.QuotationId).HasDatabaseName("IX_QuotationAttachment_Quotation");

        builder.HasOne(x => x.Quotation).WithMany(x => x.Attachments)
            .HasForeignKey(x => x.QuotationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuotationEventConfiguration : IEntityTypeConfiguration<QuotationEvent>
{
    public void Configure(EntityTypeBuilder<QuotationEvent> builder)
    {
        builder.ToTable("TRN_QuotationEvent");
        builder.HasKey(x => x.QuotationEventId);
        builder.Property(x => x.Summary).HasMaxLength(400).IsRequired();
        builder.Property(x => x.Channel).HasMaxLength(20);
        builder.Property(x => x.Destination).HasMaxLength(256);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        // The timeline is read newest-last for one quotation, never across quotations.
        builder.HasIndex(x => new { x.QuotationId, x.CreatedUtc })
            .HasDatabaseName("IX_QuotationEvent_Quotation_Created");

        builder.HasOne(x => x.Quotation).WithMany(x => x.Events)
            .HasForeignKey(x => x.QuotationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuotationStatusHistoryConfiguration : IEntityTypeConfiguration<QuotationStatusHistory>
{
    public void Configure(EntityTypeBuilder<QuotationStatusHistory> builder)
    {
        builder.ToTable("TRN_QuotationStatusHistory");
        builder.HasKey(x => x.QuotationStatusHistoryId);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => x.QuotationId).HasDatabaseName("IX_QuotationStatusHistory_Quotation");

        builder.HasOne(x => x.Quotation).WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.QuotationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("MST_BankAccount");
        builder.HasKey(x => x.BankAccountId);
        builder.Property(x => x.AccountCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.BankName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AccountName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AccountNumberMasked).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Ifsc).HasMaxLength(20);
        builder.Property(x => x.Branch).HasMaxLength(150);
        builder.Property(x => x.AccountType).HasMaxLength(40);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => x.AccountCode).IsUnique().HasDatabaseName("UX_BankAccount_Code");

        builder.HasOne<Location>().WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        // A statement line is a credit or a debit of a real amount, and it can be
        // matched to a payment or an expense but never to both.
        builder.ToTable("TRN_BankTransaction", t =>
        {
            t.HasCheckConstraint("CK_BankTxn_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_BankTxn_SingleMatch",
                "[MatchedPaymentId] IS NULL OR [MatchedExpenseId] IS NULL");
        });
        builder.HasKey(x => x.BankTransactionId);
        builder.Property(x => x.Narration).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ReferenceNumber).HasMaxLength(80);
        builder.Property(x => x.BankReferenceId).HasMaxLength(120);
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.Property(x => x.ImportBatchId).HasMaxLength(60);
        builder.Property(x => x.Remarks).HasMaxLength(500);

        builder.HasIndex(x => new { x.BankAccountId, x.TransactionDate })
            .HasDatabaseName("IX_BankTxn_Account_Date");
        builder.HasIndex(x => new { x.ReconciliationStatus, x.TransactionDate })
            .HasDatabaseName("IX_BankTxn_Reconciliation_Date");
        // Re-importing the same statement must not duplicate a line.
        builder.HasIndex(x => new { x.BankAccountId, x.BankReferenceId }).IsUnique()
            .HasDatabaseName("UX_BankTxn_Account_BankReference")
            .HasFilter("[BankReferenceId] IS NOT NULL");
        // A payment or an expense can only be reconciled once.
        builder.HasIndex(x => x.MatchedPaymentId).IsUnique()
            .HasDatabaseName("UX_BankTxn_MatchedPayment")
            .HasFilter("[MatchedPaymentId] IS NOT NULL");
        builder.HasIndex(x => x.MatchedExpenseId).IsUnique()
            .HasDatabaseName("UX_BankTxn_MatchedExpense")
            .HasFilter("[MatchedExpenseId] IS NOT NULL");

        builder.HasOne(x => x.BankAccount).WithMany(x => x.Transactions)
            .HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        // Matching points at an existing record; it never creates or cascades into one.
        builder.HasOne<Payment>().WithMany()
            .HasForeignKey(x => x.MatchedPaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Expense>().WithMany()
            .HasForeignKey(x => x.MatchedExpenseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ExpenseAdjustmentConfiguration : IEntityTypeConfiguration<ExpenseAdjustment>
{
    public void Configure(EntityTypeBuilder<ExpenseAdjustment> builder)
    {
        builder.ToTable("TRN_ExpenseAdjustment", t =>
        {
            t.HasCheckConstraint("CK_ExpenseAdjustment_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_ExpenseAdjustment_Type", "[AdjustmentType] IN (1, 2)");
        });
        builder.HasKey(x => x.ExpenseAdjustmentId);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Remarks).HasMaxLength(500);

        builder.HasIndex(x => new { x.ExpenseId, x.AdjustmentDate })
            .HasDatabaseName("IX_ExpenseAdjustment_Expense_Date");
        builder.HasIndex(x => x.AdjustmentDate).HasDatabaseName("IX_ExpenseAdjustment_Date");

        builder.HasOne<Expense>().WithMany()
            .HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Restrict);
    }
}
