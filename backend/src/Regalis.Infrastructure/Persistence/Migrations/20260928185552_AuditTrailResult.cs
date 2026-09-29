using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrailResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChangedFields",
                table: "TRN_AuditLog",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "TRN_AuditLog",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Result",
                table: "TRN_AuditLog",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Success");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Action_Timestamp",
                table: "TRN_AuditLog",
                columns: new[] { "Action", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Module_Timestamp",
                table: "TRN_AuditLog",
                columns: new[] { "ModuleName", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Result_Timestamp",
                table: "TRN_AuditLog",
                columns: new[] { "Result", "TimestampUtc" });

            // Existing rows default to Success (spec §4.4) — except the refused-permission
            // rows, whose outcome was always a denial. Only the new columns are filled in.
            migrationBuilder.Sql("""
                UPDATE TRN_AuditLog
                SET Result = N'Denied', FailureReason = N'Missing permission: ' + ISNULL(EntityId, N'?')
                WHERE Action = N'AUTHORIZATION_FAILED' AND FailureReason IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLog_Action_Timestamp",
                table: "TRN_AuditLog");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_Module_Timestamp",
                table: "TRN_AuditLog");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_Result_Timestamp",
                table: "TRN_AuditLog");

            migrationBuilder.DropColumn(
                name: "ChangedFields",
                table: "TRN_AuditLog");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "TRN_AuditLog");

            migrationBuilder.DropColumn(
                name: "Result",
                table: "TRN_AuditLog");
        }
    }
}
