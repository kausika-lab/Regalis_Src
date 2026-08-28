using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;

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
        builder.Property(x => x.OutstandingAmount)
            .HasComputedColumnSql("[TotalAmount] - [PaidAmount]", stored: true);
        builder.Property(x => x.CustomerName).HasMaxLength(150).IsRequired();
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

        builder.HasIndex(x => x.InvoiceNumber).IsUnique().HasDatabaseName("UX_Invoice_Number");
        builder.HasIndex(x => new { x.CustomerId, x.InvoiceDate }).HasDatabaseName("IX_Invoice_Customer_Date");
        builder.HasIndex(x => new { x.Status, x.InvoiceDate }).HasDatabaseName("IX_Invoice_Status_Date");

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
