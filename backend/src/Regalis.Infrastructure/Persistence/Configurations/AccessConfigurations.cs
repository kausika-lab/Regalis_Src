using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Access;
using Regalis.Domain.Entities.Identity;

namespace Regalis.Infrastructure.Persistence.Configurations;

public class AccessTokenConfiguration : IEntityTypeConfiguration<AccessToken>
{
    public void Configure(EntityTypeBuilder<AccessToken> builder)
    {
        builder.ToTable("TRN_AccessToken");
        builder.HasKey(x => x.AccessTokenId);
        builder.Property(x => x.TokenValue).HasMaxLength(64).IsRequired();
        builder.Property(x => x.QrPayload).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RevokedReason).HasMaxLength(300);
        builder.HasIndex(x => x.TokenValue).IsUnique().HasDatabaseName("UX_AccessToken_Value");
        builder.HasIndex(x => new { x.UserId, x.Status }).HasDatabaseName("IX_AccessToken_User_Status");
        builder.HasIndex(x => x.BookingId).HasDatabaseName("IX_AccessToken_Booking");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AccessLogConfiguration : IEntityTypeConfiguration<AccessLog>
{
    public void Configure(EntityTypeBuilder<AccessLog> builder)
    {
        builder.ToTable("TRN_AccessLog");
        builder.HasKey(x => x.AccessLogId);
        builder.Property(x => x.ScannedTokenValue).HasMaxLength(64);
        builder.Property(x => x.DenyReason).HasMaxLength(200);
        builder.Property(x => x.DeviceId).HasMaxLength(80);
        builder.Property(x => x.DeviceStatus).HasMaxLength(60);
        builder.Property(x => x.OverrideReason).HasMaxLength(500);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.HasIndex(x => x.LoggedUtc).HasDatabaseName("IX_AccessLog_Logged");
        builder.HasIndex(x => new { x.UserId, x.LoggedUtc }).HasDatabaseName("IX_AccessLog_User_Logged");
        builder.HasIndex(x => new { x.Result, x.LoggedUtc }).HasDatabaseName("IX_AccessLog_Result_Logged");
    }
}

public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.ToTable("TRN_Attendance");
        builder.HasKey(x => x.AttendanceId);
        builder.Property(x => x.ExceptionNotes).HasMaxLength(500);
        builder.Property(x => x.CorrectionReason).HasMaxLength(500);
        // Single check-in per day is a configurable business rule enforced in the
        // service layer (Attendance.AllowMultipleCheckInPerDay) — index, not constraint.
        builder.HasIndex(x => new { x.UserId, x.AttendanceDate }).HasDatabaseName("IX_Attendance_User_Date");
        builder.HasIndex(x => new { x.AttendanceDate, x.LocationId }).HasDatabaseName("IX_Attendance_Date_Location");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
