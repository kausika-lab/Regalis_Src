using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Reporting;

namespace Regalis.Infrastructure.Persistence.Configurations;

public class DailyMetricConfiguration : IEntityTypeConfiguration<DailyMetric>
{
    public void Configure(EntityTypeBuilder<DailyMetric> builder)
    {
        builder.ToTable("RPT_DailyMetric");
        builder.HasKey(x => x.DailyMetricId);
        builder.Property(x => x.Category).HasMaxLength(60).IsRequired();
        // A rebuild deletes the day and inserts it again; the unique key is what
        // guarantees a re-run can never leave duplicates behind.
        builder.HasIndex(x => new { x.MetricDate, x.LocationId, x.Category }).IsUnique()
            .HasDatabaseName("UX_DailyMetric_Date_Location_Category");
    }
}

public class ReportFavouriteConfiguration : IEntityTypeConfiguration<ReportFavourite>
{
    public void Configure(EntityTypeBuilder<ReportFavourite> builder)
    {
        builder.ToTable("RPT_Favourite");
        builder.HasKey(x => new { x.UserId, x.ReportKey });
        builder.Property(x => x.ReportKey).HasMaxLength(60);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReportPreferenceConfiguration : IEntityTypeConfiguration<ReportPreference>
{
    public void Configure(EntityTypeBuilder<ReportPreference> builder)
    {
        builder.ToTable("RPT_Preference");
        builder.HasKey(x => new { x.UserId, x.ReportKey });
        builder.Property(x => x.ReportKey).HasMaxLength(60);
        builder.Property(x => x.FiltersJson).HasMaxLength(4000);
        builder.Property(x => x.ColumnsJson).HasMaxLength(2000);
        builder.Property(x => x.SortJson).HasMaxLength(200);
        // "Most used" across the account sums OpenCount per report.
        builder.HasIndex(x => x.ReportKey).HasDatabaseName("IX_ReportPreference_Report");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReportScheduleConfiguration : IEntityTypeConfiguration<ReportSchedule>
{
    public void Configure(EntityTypeBuilder<ReportSchedule> builder)
    {
        builder.ToTable("RPT_Schedule");
        builder.HasKey(x => x.ReportScheduleId);
        builder.Property(x => x.ReportKey).HasMaxLength(60).IsRequired();
        builder.Property(x => x.FiltersJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Recipients).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(500);
        builder.Property(x => x.DisabledReason).HasMaxLength(300);
        builder.Property(x => x.TimeIst).HasColumnType("time(0)");
        // The worker asks "what is due?" every minute.
        builder.HasIndex(x => new { x.IsActive, x.NextRunUtc }).HasDatabaseName("IX_ReportSchedule_Due");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ReportScheduleRunConfiguration : IEntityTypeConfiguration<ReportScheduleRun>
{
    public void Configure(EntityTypeBuilder<ReportScheduleRun> builder)
    {
        builder.ToTable("RPT_ScheduleRun");
        builder.HasKey(x => x.ReportScheduleRunId);
        builder.Property(x => x.Recipients).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(500);
        builder.HasIndex(x => new { x.ReportScheduleId, x.RunUtc }).HasDatabaseName("IX_ReportScheduleRun_Schedule");
        builder.HasOne<ReportSchedule>().WithMany().HasForeignKey(x => x.ReportScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReportExportConfiguration : IEntityTypeConfiguration<ReportExport>
{
    public void Configure(EntityTypeBuilder<ReportExport> builder)
    {
        builder.ToTable("RPT_Export");
        builder.HasKey(x => x.ReportExportId);
        builder.Property(x => x.ReportKey).HasMaxLength(60).IsRequired();
        builder.Property(x => x.FiltersJson).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.CreatedUtc }).HasDatabaseName("IX_ReportExport_User");
        // The log outlives the account: no cascade.
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ReportExportJobConfiguration : IEntityTypeConfiguration<ReportExportJob>
{
    public void Configure(EntityTypeBuilder<ReportExportJob> builder)
    {
        builder.ToTable("RPT_ExportJob");
        builder.HasKey(x => x.ReportExportJobId);
        builder.Property(x => x.ReportKey).HasMaxLength(60).IsRequired();
        builder.Property(x => x.FiltersJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Token).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(200);
        builder.Property(x => x.FilePath).HasMaxLength(500);
        builder.Property(x => x.PortalBaseUrl).HasMaxLength(200);
        builder.Property(x => x.Error).HasMaxLength(500);
        builder.HasIndex(x => x.Token).IsUnique().HasDatabaseName("UX_ReportExportJob_Token");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_ReportExportJob_Status");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
