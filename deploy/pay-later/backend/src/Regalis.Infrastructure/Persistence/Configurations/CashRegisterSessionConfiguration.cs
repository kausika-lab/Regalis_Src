using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Masters;

namespace Regalis.Infrastructure.Persistence.Configurations;

public class CashRegisterSessionConfiguration : IEntityTypeConfiguration<CashRegisterSession>
{
    public void Configure(EntityTypeBuilder<CashRegisterSession> builder)
    {
        builder.ToTable("TRN_CashRegisterSession", t =>
        {
            t.HasCheckConstraint("CK_CashSession_Float", "[OpeningFloat] >= 0");
            t.HasCheckConstraint("CK_CashSession_Closed",
                "[Status] = 1 OR ([ClosedUtc] IS NOT NULL AND [ExpectedCash] IS NOT NULL AND [CountedCash] IS NOT NULL)");
            t.HasCheckConstraint("CK_CashSession_VarianceReason",
                "[Status] = 1 OR [CountedCash] = [ExpectedCash] OR [VarianceReason] IS NOT NULL");
        });
        builder.HasKey(x => x.CashRegisterSessionId);
        builder.Property(x => x.SessionNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.VarianceReason).HasMaxLength(500);
        builder.Property(x => x.Variance).HasComputedColumnSql("[CountedCash] - [ExpectedCash]", stored: true);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.SessionNumber).IsUnique().HasDatabaseName("UX_CashSession_Number");
        builder.HasIndex(x => new { x.LocationId, x.UserId, x.BusinessDate }).IsUnique()
            .HasDatabaseName("UX_CashSession_Location_User_Date");
        builder.HasIndex(x => new { x.Status, x.BusinessDate }).HasDatabaseName("IX_CashSession_Status_Date");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.HandedOverToUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
