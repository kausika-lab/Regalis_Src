using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MST_HousekeeperProfile",
                columns: table => new
                {
                    HousekeeperProfileId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    HousekeeperCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Shift = table.Column<byte>(type: "tinyint", nullable: false),
                    AssignedAreas = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    JoiningDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_HousekeeperProfile", x => x.HousekeeperProfileId);
                    table.ForeignKey(
                        name: "FK_MST_HousekeeperProfile_MST_User_UserId",
                        column: x => x.UserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_MaintenanceWorkItem",
                columns: table => new
                {
                    MaintenanceWorkItemId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    WorkspaceId = table.Column<int>(type: "int", nullable: true),
                    AreaName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Priority = table.Column<byte>(type: "tinyint", nullable: false),
                    AssignedToUserId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    DueUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CompletedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CompletionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PhotoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PhotoFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    PhotoContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VerifiedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    VerifiedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReworkReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReworkCount = table.Column<int>(type: "int", nullable: false),
                    OverdueSinceUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancelledUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LinkedScheduleId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_MaintenanceWorkItem", x => x.MaintenanceWorkItemId);
                    table.ForeignKey(
                        name: "FK_TRN_MaintenanceWorkItem_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_MaintenanceWorkItem_MST_User_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_MaintenanceWorkItem_TRN_HousekeepingSchedule_LinkedScheduleId",
                        column: x => x.LinkedScheduleId,
                        principalTable: "TRN_HousekeepingSchedule",
                        principalColumn: "HousekeepingScheduleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_HousekeeperProfile_Code",
                table: "MST_HousekeeperProfile",
                column: "HousekeeperCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_HousekeeperProfile_User",
                table: "MST_HousekeeperProfile",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWork_Assignee_Status",
                table: "TRN_MaintenanceWorkItem",
                columns: new[] { "AssignedToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWork_Due_Status",
                table: "TRN_MaintenanceWorkItem",
                columns: new[] { "DueUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_MaintenanceWorkItem_LinkedScheduleId",
                table: "TRN_MaintenanceWorkItem",
                column: "LinkedScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_MaintenanceWorkItem_LocationId",
                table: "TRN_MaintenanceWorkItem",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "UX_MaintenanceWork_Number",
                table: "TRN_MaintenanceWorkItem",
                column: "WorkNumber",
                unique: true);

            // Sequences for MW-000001 / HK-000001. The seeder adds them on a fresh database,
            // but UAT runs without it; the backfill below needs Housekeeper to exist.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM MST_NumberSequence WHERE SequenceKey = N'MaintenanceWork' AND FinancialYear IS NULL)
                    INSERT INTO MST_NumberSequence (SequenceKey, Prefix, CurrentValue, FinancialYear) VALUES (N'MaintenanceWork', N'MW-', 0, NULL);
                IF NOT EXISTS (SELECT 1 FROM MST_NumberSequence WHERE SequenceKey = N'Housekeeper' AND FinancialYear IS NULL)
                    INSERT INTO MST_NumberSequence (SequenceKey, Prefix, CurrentValue, FinancialYear) VALUES (N'Housekeeper', N'HK-', 0, NULL);
                """);

            // Existing Housekeeping Staff users become housekeepers (Morning shift, joined on
            // their account date) so they can be assigned work straight away.
            migrationBuilder.Sql("""
                DECLARE @start bigint = (SELECT CurrentValue FROM MST_NumberSequence WHERE SequenceKey = N'Housekeeper' AND FinancialYear IS NULL);
                ;WITH hk AS (
                    SELECT DISTINCT u.UserId, u.CreatedDate
                    FROM MST_User u
                    JOIN MAP_UserRole ur ON ur.UserId = u.UserId
                    JOIN MST_Role r ON r.RoleId = ur.RoleId
                    WHERE r.RoleCode = N'HOUSEKEEPING' AND u.IsDeleted = 0
                      AND NOT EXISTS (SELECT 1 FROM MST_HousekeeperProfile p WHERE p.UserId = u.UserId)
                )
                INSERT INTO MST_HousekeeperProfile (UserId, HousekeeperCode, Shift, AssignedAreas, JoiningDate, CreatedDate, IsActive, IsDeleted)
                SELECT UserId,
                       N'HK-' + RIGHT(N'000000' + CAST(@start + ROW_NUMBER() OVER (ORDER BY UserId) AS nvarchar(20)), 6),
                       1, NULL, CAST(CreatedDate AS date), SYSUTCDATETIME(), 1, 0
                FROM hk;
                UPDATE MST_NumberSequence SET CurrentValue = CurrentValue + @@ROWCOUNT
                WHERE SequenceKey = N'Housekeeper' AND FinancialYear IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MST_HousekeeperProfile");

            migrationBuilder.DropTable(
                name: "TRN_MaintenanceWorkItem");
        }
    }
}
