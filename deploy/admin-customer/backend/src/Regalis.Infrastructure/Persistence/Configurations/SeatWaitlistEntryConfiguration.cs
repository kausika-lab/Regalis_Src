using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Entities.Waitlist;

namespace Regalis.Infrastructure.Persistence.Configurations;

/// <summary>TRN_SeatWaitlist. The table is created by
/// db/migrations/20261113120000_AdminCustomerChanges.sql (no EF migration; see the kit README).</summary>
public class SeatWaitlistEntryConfiguration : IEntityTypeConfiguration<SeatWaitlistEntry>
{
	public void Configure(EntityTypeBuilder<SeatWaitlistEntry> builder)
	{
		builder.ToTable("TRN_SeatWaitlist");
		builder.HasKey(x => x.SeatWaitlistId);
		builder.Property(x => x.Status).HasConversion<byte>();
		builder.Property(x => x.CloseReason).HasMaxLength(300);
		builder.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<WorkspaceType>().WithMany().HasForeignKey(x => x.WorkspaceTypeId).OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(x => new { x.Status, x.WaitDate }).HasDatabaseName("IX_SeatWaitlist_Status_Date");
		builder.HasIndex(x => new { x.CustomerId, x.Status }).HasDatabaseName("IX_SeatWaitlist_Customer");
		builder.HasIndex(x => new { x.CustomerId, x.LocationId, x.WorkspaceTypeId, x.WaitDate })
			.IsUnique()
			.HasFilter("[Status] = 1 AND [IsDeleted] = 0")
			.HasDatabaseName("UX_SeatWaitlist_OpenEntry");
	}
}
