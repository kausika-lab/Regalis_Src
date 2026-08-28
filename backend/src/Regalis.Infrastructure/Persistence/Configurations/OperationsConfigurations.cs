using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Entities.Operations;

namespace Regalis.Infrastructure.Persistence.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("MST_InventoryItem");
        builder.HasKey(x => x.InventoryItemId);
        builder.Property(x => x.ItemCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ItemName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(60);
        builder.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        builder.Property(x => x.OpeningQuantity).HasPrecision(18, 3);
        builder.Property(x => x.ReorderLevel).HasPrecision(18, 3);
        builder.Property(x => x.ImagePath).HasMaxLength(500);
        builder.HasIndex(x => x.ItemCode).IsUnique().HasDatabaseName("UX_InventoryItem_Code");

        builder.HasOne<Location>().WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("TRN_InventoryTransaction", t =>
            t.HasCheckConstraint("CK_InvTxn_Quantity", "[Quantity] > 0"));
        builder.HasKey(x => x.InventoryTransactionId);
        builder.Property(x => x.TransactionNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.SourceOrDestination).HasMaxLength(200);
        builder.Property(x => x.ReferenceNumber).HasMaxLength(80);
        builder.Property(x => x.DamageDescription).HasMaxLength(500);
        builder.Property(x => x.DamageCause).HasMaxLength(300);
        builder.Property(x => x.PhotoPath).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.TransactionNumber).IsUnique().HasDatabaseName("UX_InvTxn_Number");
        builder.HasIndex(x => new { x.InventoryItemId, x.TransactionDate }).HasDatabaseName("IX_InvTxn_Item_Date");
        builder.HasIndex(x => new { x.TransactionType, x.TransactionDate }).HasDatabaseName("IX_InvTxn_Type_Date");

        builder.HasOne(x => x.InventoryItem).WithMany(x => x.Transactions)
            .HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class HousekeepingScheduleConfiguration : IEntityTypeConfiguration<HousekeepingSchedule>
{
    public void Configure(EntityTypeBuilder<HousekeepingSchedule> builder)
    {
        builder.ToTable("TRN_HousekeepingSchedule");
        builder.HasKey(x => x.HousekeepingScheduleId);
        builder.Property(x => x.ScheduleNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.AreaName).HasMaxLength(150);
        builder.Property(x => x.TaskDescription).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CompletionNotes).HasMaxLength(500);
        builder.HasIndex(x => x.ScheduleNumber).IsUnique().HasDatabaseName("UX_HkSchedule_Number");
        builder.HasIndex(x => new { x.StaffUserId, x.ScheduledDate }).HasDatabaseName("IX_HkSchedule_Staff_Date");
        builder.HasIndex(x => new { x.ScheduledDate, x.Status }).HasDatabaseName("IX_HkSchedule_Date_Status");
        builder.HasIndex(x => new { x.WorkspaceId, x.StartUtc, x.EndUtc }).HasDatabaseName("IX_HkSchedule_Workspace_Period");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.StaffUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("TRN_Notification");
        builder.HasKey(x => x.NotificationId);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(40);
        builder.Property(x => x.ActionLabel).HasMaxLength(60);
        builder.Property(x => x.ActionRoute).HasMaxLength(200);
        builder.HasIndex(x => new { x.UserId, x.CreatedDate }).HasDatabaseName("IX_Notification_User_Created");
        builder.HasIndex(x => new { x.UserId, x.IsRead }).HasDatabaseName("IX_Notification_User_IsRead");
        builder.HasIndex(x => x.ScheduledForUtc).HasDatabaseName("IX_Notification_Scheduled");
    }
}

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("TRN_Ticket");
        builder.HasKey(x => x.TicketId);
        builder.Property(x => x.TicketNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.TicketNumber).IsUnique().HasDatabaseName("UX_Ticket_Number");
        builder.HasIndex(x => new { x.CustomerId, x.CreatedDate }).HasDatabaseName("IX_Ticket_Customer_Created");
        builder.HasIndex(x => new { x.Status, x.Priority }).HasDatabaseName("IX_Ticket_Status_Priority");
        builder.HasIndex(x => new { x.AssignedToUserId, x.Status }).HasDatabaseName("IX_Ticket_Assigned_Status");

        builder.HasOne(x => x.TicketCategory).WithMany()
            .HasForeignKey(x => x.TicketCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TicketIssueType).WithMany()
            .HasForeignKey(x => x.TicketIssueTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("TRN_TicketMessage");
        builder.HasKey(x => x.TicketMessageId);

        builder.HasOne(x => x.Ticket).WithMany(x => x.Messages)
            .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> builder)
    {
        builder.ToTable("TRN_TicketAttachment");
        builder.HasKey(x => x.TicketAttachmentId);
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.StoredFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();

        builder.HasOne(x => x.Ticket).WithMany(x => x.Attachments)
            .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}
