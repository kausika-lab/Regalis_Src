using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Masters;

namespace Regalis.Infrastructure.Persistence.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("MST_Location");
        builder.HasKey(x => x.LocationId);
        builder.Property(x => x.LocationCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.LocationName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AddressLine1).HasMaxLength(200);
        builder.Property(x => x.AddressLine2).HasMaxLength(200);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(100);
        builder.Property(x => x.PostalCode).HasMaxLength(20);
        builder.Property(x => x.ContactNumber).HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.WorkingHoursStart).HasColumnType("time(0)");
        builder.Property(x => x.WorkingHoursEnd).HasColumnType("time(0)");
        builder.Property(x => x.WorkingDays).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Latitude).HasPrecision(9, 6);
        builder.Property(x => x.Longitude).HasPrecision(9, 6);
        builder.Property(x => x.ImagePath).HasMaxLength(500);
        builder.HasIndex(x => x.LocationCode).IsUnique().HasDatabaseName("UX_Location_Code");
    }
}

public class WorkspaceTypeConfiguration : IEntityTypeConfiguration<WorkspaceType>
{
    public void Configure(EntityTypeBuilder<WorkspaceType> builder)
    {
        builder.ToTable("MST_WorkspaceType");
        builder.HasKey(x => x.WorkspaceTypeId);
        builder.Property(x => x.TypeCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.TypeName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IconName).HasMaxLength(60);
        builder.HasIndex(x => x.TypeCode).IsUnique().HasDatabaseName("UX_WorkspaceType_Code");
    }
}

public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("MST_Workspace", t =>
        {
            t.HasCheckConstraint("CK_Workspace_Capacity", "[Capacity] >= 1");
            t.HasCheckConstraint("CK_Workspace_Price", "[PricePerHour] >= 0");
        });
        builder.HasKey(x => x.WorkspaceId);
        builder.Property(x => x.WorkspaceCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.WorkspaceName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.FloorNumber).HasMaxLength(20);
        builder.Property(x => x.Equipment).HasMaxLength(500);
        builder.Property(x => x.ImagePath).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.LocationId, x.WorkspaceCode }).IsUnique()
            .HasDatabaseName("UX_Workspace_Location_Code");

        builder.HasOne(x => x.Location).WithMany(x => x.Workspaces)
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.WorkspaceType).WithMany(x => x.Workspaces)
            .HasForeignKey(x => x.WorkspaceTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AmenityConfiguration : IEntityTypeConfiguration<Amenity>
{
    public void Configure(EntityTypeBuilder<Amenity> builder)
    {
        builder.ToTable("MST_Amenity");
        builder.HasKey(x => x.AmenityId);
        builder.Property(x => x.AmenityName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IconName).HasMaxLength(60);
    }
}

public class WorkspaceAmenityConfiguration : IEntityTypeConfiguration<WorkspaceAmenity>
{
    public void Configure(EntityTypeBuilder<WorkspaceAmenity> builder)
    {
        builder.ToTable("MAP_WorkspaceAmenity");
        builder.HasKey(x => x.WorkspaceAmenityId);
        builder.HasIndex(x => new { x.WorkspaceId, x.AmenityId }).IsUnique()
            .HasDatabaseName("UX_WorkspaceAmenity");

        builder.HasOne(x => x.Workspace).WithMany(x => x.WorkspaceAmenities)
            .HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Amenity).WithMany(x => x.WorkspaceAmenities)
            .HasForeignKey(x => x.AmenityId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PassPlanConfiguration : IEntityTypeConfiguration<PassPlan>
{
    public void Configure(EntityTypeBuilder<PassPlan> builder)
    {
        builder.ToTable("MST_PassPlan");
        builder.HasKey(x => x.PassPlanId);
        builder.Property(x => x.PlanCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PlanName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ImagePath).HasMaxLength(500);
        builder.HasIndex(x => x.PlanCode).IsUnique().HasDatabaseName("UX_PassPlan_Code");

        builder.HasOne(x => x.Location).WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ServiceItemConfiguration : IEntityTypeConfiguration<ServiceItem>
{
    public void Configure(EntityTypeBuilder<ServiceItem> builder)
    {
        builder.ToTable("MST_ServiceItem");
        builder.HasKey(x => x.ServiceItemId);
        builder.Property(x => x.ItemCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ItemName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(20);
        builder.Property(x => x.ImagePath).HasMaxLength(500);
        builder.HasIndex(x => x.ItemCode).IsUnique().HasDatabaseName("UX_ServiceItem_Code");

        builder.HasOne(x => x.Location).WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> builder)
    {
        builder.ToTable("MST_Banner");
        builder.HasKey(x => x.BannerId);
        builder.Property(x => x.Title).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Subtitle).HasMaxLength(300);
        builder.Property(x => x.ImagePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.LinkUrl).HasMaxLength(500);

        builder.HasOne(x => x.Location).WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("MST_AppSetting");
        builder.HasKey(x => x.AppSettingId);
        builder.Property(x => x.SettingKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.SettingGroup).HasMaxLength(60).IsRequired();
        builder.Property(x => x.DataType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300);
        builder.HasIndex(x => x.SettingKey).IsUnique().HasDatabaseName("UX_AppSetting_Key");
    }
}

public class TicketCategoryConfiguration : IEntityTypeConfiguration<TicketCategory>
{
    public void Configure(EntityTypeBuilder<TicketCategory> builder)
    {
        builder.ToTable("MST_TicketCategory");
        builder.HasKey(x => x.TicketCategoryId);
        builder.Property(x => x.CategoryName).HasMaxLength(120).IsRequired();
    }
}

public class TicketIssueTypeConfiguration : IEntityTypeConfiguration<TicketIssueType>
{
    public void Configure(EntityTypeBuilder<TicketIssueType> builder)
    {
        builder.ToTable("MST_TicketIssueType");
        builder.HasKey(x => x.TicketIssueTypeId);
        builder.Property(x => x.IssueTypeName).HasMaxLength(150).IsRequired();

        builder.HasOne(x => x.TicketCategory).WithMany(x => x.IssueTypes)
            .HasForeignKey(x => x.TicketCategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.ToTable("MST_NumberSequence");
        builder.HasKey(x => x.NumberSequenceId);
        builder.Property(x => x.SequenceKey).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Prefix).HasMaxLength(30).IsRequired();
        builder.Property(x => x.FinancialYear).HasMaxLength(10);
        builder.HasIndex(x => new { x.SequenceKey, x.FinancialYear }).IsUnique()
            .HasDatabaseName("UX_NumberSequence_Key_Year");
    }
}
