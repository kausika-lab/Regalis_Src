using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data-only. Invoice.NumberPrefix and Booking.NumberPrefix were seeded into
    /// MST_AppSetting but nothing ever read them — the prefix a number actually uses
    /// comes from MST_NumberSequence.Prefix. A setting an administrator can edit that
    /// silently changes nothing is worse than no setting at all, so the rows go and
    /// number prefixes live in exactly one place.
    /// </summary>
    public partial class RemoveDeadNumberPrefixSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM dbo.MST_AppSetting
                 WHERE SettingKey IN ('Invoice.NumberPrefix', 'Booking.NumberPrefix');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the rows exactly as SeedCatalogue used to create them, so a
            // rollback lands on the previous state rather than a half-seeded one.
            migrationBuilder.Sql("""
                INSERT INTO dbo.MST_AppSetting
                    (SettingKey, SettingValue, SettingGroup, DataType, Description,
                     IsEditableByAdmin, IsSecret, IsActive, IsDeleted, CreatedDate)
                SELECT 'Invoice.NumberPrefix', 'REG/INV', 'Invoice', 'string',
                       'Invoice number prefix', 1, 0, 1, 0, SYSUTCDATETIME()
                 WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_AppSetting
                                    WHERE SettingKey = 'Invoice.NumberPrefix');

                INSERT INTO dbo.MST_AppSetting
                    (SettingKey, SettingValue, SettingGroup, DataType, Description,
                     IsEditableByAdmin, IsSecret, IsActive, IsDeleted, CreatedDate)
                SELECT 'Booking.NumberPrefix', 'REG-', 'Booking', 'string',
                       'Booking number prefix', 1, 0, 1, 0, SYSUTCDATETIME()
                 WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_AppSetting
                                    WHERE SettingKey = 'Booking.NumberPrefix');
                """);
        }
    }
}
