using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Operations;

namespace Regalis.Infrastructure.Persistence.Configurations;

public class AdminNotificationConfiguration : IEntityTypeConfiguration<AdminNotification>
{
    public void Configure(EntityTypeBuilder<AdminNotification> builder)
    {
        builder.ToTable("TRN_AdminNotification");
        builder.HasKey(x => x.AdminNotificationId);
        builder.Property(x => x.EventCode).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(40);
        builder.Property(x => x.ReferenceLabel).HasMaxLength(120);
        builder.Property(x => x.ActionLabel).HasMaxLength(60);
        builder.Property(x => x.ActionRoute).HasMaxLength(300);
        builder.Property(x => x.DeliveryNote).HasMaxLength(300);
        builder.HasIndex(x => x.CreatedUtc).HasDatabaseName("IX_AdminNotification_Created");
        builder.HasIndex(x => new { x.Type, x.CreatedUtc }).HasDatabaseName("IX_AdminNotification_Type_Created");
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId, x.EventCode })
            .HasDatabaseName("IX_AdminNotification_Reference");
        // The delivery worker picks up rows it has not handled yet.
        builder.HasIndex(x => x.DeliveredUtc).HasDatabaseName("IX_AdminNotification_Undelivered")
            .HasFilter("[DeliveredUtc] IS NULL");
        // A customer that is later deleted keeps its history: no cascade.
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AdminNotificationReadConfiguration : IEntityTypeConfiguration<AdminNotificationRead>
{
    public void Configure(EntityTypeBuilder<AdminNotificationRead> builder)
    {
        builder.ToTable("TRN_AdminNotificationRead");
        builder.HasKey(x => new { x.AdminNotificationId, x.UserId });
        builder.HasIndex(x => x.UserId).HasDatabaseName("IX_AdminNotificationRead_User");
        builder.HasOne(x => x.Notification).WithMany(x => x.Reads)
            .HasForeignKey(x => x.AdminNotificationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AdminNotificationSettingConfiguration : IEntityTypeConfiguration<AdminNotificationSetting>
{
    public void Configure(EntityTypeBuilder<AdminNotificationSetting> builder)
    {
        builder.ToTable("MST_AdminNotificationSetting");
        builder.HasKey(x => new { x.UserId, x.Type });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("TRN_Announcement");
        builder.HasKey(x => x.AnnouncementId);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.Status, x.ScheduledForUtc }).HasDatabaseName("IX_Announcement_Status_Scheduled");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.TargetCustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
