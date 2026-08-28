using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Finance;

namespace Regalis.Infrastructure.Persistence.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("TRN_Booking", t =>
        {
            t.HasCheckConstraint("CK_Booking_Period", "[EndUtc] > [StartUtc]");
            t.HasCheckConstraint("CK_Booking_Amounts",
                "[BaseAmount] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] >= 0");
        });
        builder.HasKey(x => x.BookingId);
        builder.Property(x => x.BookingNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.BookingNumber).IsUnique().HasDatabaseName("UX_Booking_Number");

        // The overlap-detection index; leading columns match the conflict predicate.
        builder.HasIndex(x => new { x.WorkspaceId, x.StartUtc, x.EndUtc })
            .HasDatabaseName("IX_Booking_Workspace_Period")
            .IncludeProperties(x => new { x.Status, x.BookingId })
            .HasFilter("[IsDeleted] = 0");

        // Duplicate-submission guard for occupancy statuses (Pending, Confirmed, CheckedIn).
        builder.HasIndex(x => new { x.WorkspaceId, x.StartUtc }).IsUnique()
            .HasDatabaseName("UX_Booking_Workspace_Start")
            .HasFilter("[Status] IN (1,2,3) AND [IsDeleted] = 0");

        builder.HasIndex(x => new { x.CustomerId, x.StartUtc })
            .HasDatabaseName("IX_Booking_Customer_Start")
            .IsDescending(false, true);
        builder.HasIndex(x => new { x.BookingDate, x.Status })
            .HasDatabaseName("IX_Booking_Date_Status")
            .IncludeProperties(x => new { x.LocationId, x.TotalAmount });
        builder.HasIndex(x => new { x.LocationId, x.StartUtc })
            .HasDatabaseName("IX_Booking_Location_Start");

        builder.HasOne(x => x.Customer).WithMany()
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Location).WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Workspace).WithMany()
            .HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.WorkspaceType).WithMany()
            .HasForeignKey(x => x.WorkspaceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerPass>().WithMany()
            .HasForeignKey(x => x.CustomerPassId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BookingStatusHistoryConfiguration : IEntityTypeConfiguration<BookingStatusHistory>
{
    public void Configure(EntityTypeBuilder<BookingStatusHistory> builder)
    {
        builder.ToTable("TRN_BookingStatusHistory");
        builder.HasKey(x => x.BookingStatusHistoryId);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => x.BookingId).HasDatabaseName("IX_BookingStatusHistory_Booking");

        builder.HasOne(x => x.Booking).WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkspaceBlockConfiguration : IEntityTypeConfiguration<WorkspaceBlock>
{
    public void Configure(EntityTypeBuilder<WorkspaceBlock> builder)
    {
        builder.ToTable("TRN_WorkspaceBlock", t =>
            t.HasCheckConstraint("CK_Block_Period", "[EndUtc] > [StartUtc]"));
        builder.HasKey(x => x.WorkspaceBlockId);
        builder.Property(x => x.ReasonNotes).HasMaxLength(500);

        builder.HasIndex(x => new { x.WorkspaceId, x.StartUtc, x.EndUtc })
            .HasDatabaseName("IX_Block_Workspace_Period")
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");

        builder.HasOne(x => x.Workspace).WithMany()
            .HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Location).WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
