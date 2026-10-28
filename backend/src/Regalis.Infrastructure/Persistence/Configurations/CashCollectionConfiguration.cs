using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Masters;

namespace Regalis.Infrastructure.Persistence.Configurations;

public class CashCollectionConfiguration : IEntityTypeConfiguration<CashCollection>
{
    public void Configure(EntityTypeBuilder<CashCollection> builder)
    {
        builder.ToTable("TRN_CashCollection", t =>
        {
            t.HasCheckConstraint("CK_CashCollection_Amount", "[Amount] <> 0");
            t.HasCheckConstraint("CK_CashCollection_Method", "[PaymentMethod] IN (2,3,4,6)");
            t.HasCheckConstraint("CK_CashCollection_Reference", "[PaymentMethod] = 6 OR [ReferenceNumber] IS NOT NULL");
            t.HasCheckConstraint("CK_CashCollection_Change",
                "[AmountTendered] IS NULL OR ([PaymentMethod] = 6 AND [AmountTendered] - ISNULL([ChangeReturned], 0) = [Amount])");
            t.HasCheckConstraint("CK_CashCollection_Void",
                "([Status] = 2 AND [VoidedByUserId] IS NOT NULL AND [VoidedUtc] IS NOT NULL AND [VoidReason] IS NOT NULL) OR ([Status] = 1 AND [VoidedUtc] IS NULL)");
            t.HasCheckConstraint("CK_CashCollection_Target",
                "([BookingId] IS NOT NULL AND [CustomerPassId] IS NULL) OR ([BookingId] IS NULL AND [CustomerPassId] IS NOT NULL)");
        });
        builder.HasKey(x => x.CashCollectionId);
        builder.Property(x => x.ReceiptNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReferenceNumber).HasMaxLength(50);
        builder.Property(x => x.VoidReason).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.ReceiptNumber).IsUnique().HasDatabaseName("UX_CashCollection_Receipt");
        builder.HasIndex(x => x.BookingId).HasDatabaseName("IX_CashCollection_Booking");
        builder.HasIndex(x => new { x.LocationId, x.CollectedUtc }).HasDatabaseName("IX_CashCollection_Location_Date");
        builder.HasIndex(x => new { x.CollectedByUserId, x.CollectedUtc }).HasDatabaseName("IX_CashCollection_Collector_Date");
        builder.HasIndex(x => x.CashRegisterSessionId).HasDatabaseName("IX_CashCollection_Session");
        builder.HasIndex(x => x.CustomerPassId).HasDatabaseName("IX_CashCollection_CustomerPass");
        builder.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CustomerPass).WithMany().HasForeignKey(x => x.CustomerPassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CollectedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CashRegisterSession).WithMany(x => x.Collections)
            .HasForeignKey(x => x.CashRegisterSessionId).OnDelete(DeleteBehavior.Restrict);
    }
}
