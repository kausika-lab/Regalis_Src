using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Identity;

namespace Regalis.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("MST_User", t =>
            t.HasCheckConstraint("CK_User_UserType", "[UserType] IN (1,2,3)"));
        builder.HasKey(x => x.UserId);

        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.MobileNumber).HasMaxLength(20).IsRequired();
        // Nullable: a Google-only account has no password. See User.PasswordHash.
        builder.Property(x => x.PasswordHash).HasMaxLength(400);
        builder.Property(x => x.GoogleId).HasMaxLength(64);
        builder.Property(x => x.CompanyName).HasMaxLength(200);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.PhotoPath).HasMaxLength(500);

        // Structured profile. Lengths follow MST_Location for the address parts and
        // MST_Supplier.Gstin for the tax number, so MST_User reads the same way.
        builder.Property(x => x.AddressLine1).HasMaxLength(200);
        builder.Property(x => x.AddressLine2).HasMaxLength(200);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(100);
        builder.Property(x => x.Country).HasMaxLength(100);
        builder.Property(x => x.PostalCode).HasMaxLength(20);
        builder.Property(x => x.Gstin).HasMaxLength(20);
        // Derived from the columns above, never stored — one definition, on the entity.
        builder.Ignore(x => x.IsProfileComplete);

        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UX_User_Email")
            .HasFilter("[IsDeleted] = 0");
        // Google sign-up collects no phone number, so those rows carry the empty
        // string. Without excluding it the second such account breaks the index.
        builder.HasIndex(x => x.MobileNumber).IsUnique().HasDatabaseName("UX_User_Mobile")
            .HasFilter("[IsDeleted] = 0 AND [MobileNumber] <> ''");
        // Two accounts must never claim the same Google subject.
        builder.HasIndex(x => x.GoogleId).IsUnique().HasDatabaseName("UX_User_GoogleId")
            .HasFilter("[GoogleId] IS NOT NULL");

        builder.HasOne(x => x.DefaultLocation).WithMany()
            .HasForeignKey(x => x.DefaultLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("MST_Role");
        builder.HasKey(x => x.RoleId);
        builder.Property(x => x.RoleCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RoleName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300);
        builder.HasIndex(x => x.RoleCode).IsUnique().HasDatabaseName("UX_Role_Code");
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("MST_Permission");
        builder.HasKey(x => x.PermissionId);
        builder.Property(x => x.PermissionCode).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ModuleName).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ActionName).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.HasIndex(x => x.PermissionCode).IsUnique().HasDatabaseName("UX_Permission_Code");
    }
}

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.ToTable("MST_Menu");
        builder.HasKey(x => x.MenuId);
        builder.Property(x => x.MenuCode).HasMaxLength(60).IsRequired();
        builder.Property(x => x.MenuName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Route).HasMaxLength(200);
        builder.Property(x => x.Icon).HasMaxLength(60);
        builder.HasIndex(x => x.MenuCode).IsUnique().HasDatabaseName("UX_Menu_Code");

        // NO ACTION; cycles are prevented in code (DATABASE_DESIGN §3).
        builder.HasOne(x => x.ParentMenu).WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentMenuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Permission).WithMany()
            .HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("MAP_UserRole");
        builder.HasKey(x => x.UserRoleId);
        builder.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique().HasDatabaseName("UX_UserRole");

        builder.HasOne(x => x.User).WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Role).WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("MAP_RolePermission");
        builder.HasKey(x => x.RolePermissionId);
        builder.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique().HasDatabaseName("UX_RolePermission");

        builder.HasOne(x => x.Role).WithMany(x => x.RolePermissions)
            .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Permission).WithMany(x => x.RolePermissions)
            .HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RoleMenuConfiguration : IEntityTypeConfiguration<RoleMenu>
{
    public void Configure(EntityTypeBuilder<RoleMenu> builder)
    {
        builder.ToTable("MAP_RoleMenu");
        builder.HasKey(x => x.RoleMenuId);
        builder.HasIndex(x => new { x.RoleId, x.MenuId }).IsUnique().HasDatabaseName("UX_RoleMenu");

        builder.HasOne(x => x.Role).WithMany(x => x.RoleMenus)
            .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Menu).WithMany(x => x.RoleMenus)
            .HasForeignKey(x => x.MenuId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("TRN_RefreshToken");
        builder.HasKey(x => x.RefreshTokenId);
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CreatedByIp).HasMaxLength(64);
        builder.Property(x => x.RevokedReason).HasMaxLength(200);
        builder.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("UX_RefreshToken_Hash");
        builder.HasIndex(x => new { x.UserId, x.FamilyId }).HasDatabaseName("IX_RefreshToken_User_Family");

        builder.HasOne(x => x.User).WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OtpConfiguration : IEntityTypeConfiguration<Otp>
{
    public void Configure(EntityTypeBuilder<Otp> builder)
    {
        builder.ToTable("TRN_Otp");
        builder.HasKey(x => x.OtpId);
        builder.Property(x => x.Destination).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CodeHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.Destination, x.Purpose }).HasDatabaseName("IX_Otp_Destination_Purpose");
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("TRN_AuditLog");
        builder.HasKey(x => x.AuditLogId);
        builder.Property(x => x.UserName).HasMaxLength(150);
        builder.Property(x => x.Action).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ModuleName).HasMaxLength(60).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(100);
        builder.Property(x => x.EntityId).HasMaxLength(60);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(300);
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.HasIndex(x => x.TimestampUtc).HasDatabaseName("IX_AuditLog_Timestamp");
        builder.HasIndex(x => new { x.UserId, x.TimestampUtc }).HasDatabaseName("IX_AuditLog_User_Timestamp");
        builder.HasIndex(x => new { x.EntityName, x.EntityId }).HasDatabaseName("IX_AuditLog_Entity");
    }
}

public class PasswordHistoryConfiguration : IEntityTypeConfiguration<PasswordHistory>
{
    public void Configure(EntityTypeBuilder<PasswordHistory> builder)
    {
        builder.ToTable("TRN_PasswordHistory");
        builder.HasKey(x => x.PasswordHistoryId);
        builder.Property(x => x.PasswordHash).HasMaxLength(400).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.ChangedUtc }).HasDatabaseName("IX_PasswordHistory_User_Changed");

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LoginHistoryConfiguration : IEntityTypeConfiguration<LoginHistory>
{
    public void Configure(EntityTypeBuilder<LoginHistory> builder)
    {
        builder.ToTable("TRN_LoginHistory");
        builder.HasKey(x => x.LoginHistoryId);
        builder.Property(x => x.AttemptedEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.FailureReason).HasMaxLength(200);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(300);
        builder.HasIndex(x => x.AttemptedUtc).HasDatabaseName("IX_LoginHistory_Attempted");
    }
}
