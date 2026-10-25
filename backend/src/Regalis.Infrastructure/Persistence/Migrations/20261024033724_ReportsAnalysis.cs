using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportsAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RPT_DailyMetric",
                columns: table => new
                {
                    DailyMetricId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MetricDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    RevenuePaise = table.Column<long>(type: "bigint", nullable: false),
                    BookingsCount = table.Column<int>(type: "int", nullable: false),
                    HoursBooked = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HoursAvailable = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NewCustomers = table.Column<int>(type: "int", nullable: false),
                    CancelledCount = table.Column<int>(type: "int", nullable: false),
                    BuiltUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RPT_DailyMetric", x => x.DailyMetricId);
                });

            migrationBuilder.CreateTable(
                name: "RPT_Export",
                columns: table => new
                {
                    ReportExportId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ReportKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FiltersJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<byte>(type: "tinyint", nullable: false),
                    Delivery = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RPT_Export", x => x.ReportExportId);
                    table.ForeignKey(
                        name: "FK_RPT_Export_MST_User_UserId",
                        column: x => x.UserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RPT_ExportJob",
                columns: table => new
                {
                    ReportExportJobId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ReportKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FiltersJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Format = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PortalBaseUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RPT_ExportJob", x => x.ReportExportJobId);
                    table.ForeignKey(
                        name: "FK_RPT_ExportJob_MST_User_UserId",
                        column: x => x.UserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RPT_Favourite",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ReportKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RPT_Favourite", x => new { x.UserId, x.ReportKey });
                    table.ForeignKey(
                        name: "FK_RPT_Favourite_MST_User_UserId",
                        column: x => x.UserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RPT_Preference",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ReportKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FiltersJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ColumnsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SortJson = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OpenCount = table.Column<int>(type: "int", nullable: false),
                    LastOpenedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    LastRowCount = table.Column<int>(type: "int", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RPT_Preference", x => new { x.UserId, x.ReportKey });
                    table.ForeignKey(
                        name: "FK_RPT_Preference_MST_User_UserId",
                        column: x => x.UserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RPT_Schedule",
                columns: table => new
                {
                    ReportScheduleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FiltersJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Frequency = table.Column<byte>(type: "tinyint", nullable: false),
                    DayOfWeek = table.Column<byte>(type: "tinyint", nullable: true),
                    DayOfMonth = table.Column<byte>(type: "tinyint", nullable: true),
                    TimeIst = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    Recipients = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Format = table.Column<byte>(type: "tinyint", nullable: false),
                    OnlyIfData = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    NextRunUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    LastRunUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    LastRunStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attempt = table.Column<byte>(type: "tinyint", nullable: false),
                    RetryAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    FailingSinceUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DisabledReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RPT_Schedule", x => x.ReportScheduleId);
                    table.ForeignKey(
                        name: "FK_RPT_Schedule_MST_User_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RPT_ScheduleRun",
                columns: table => new
                {
                    ReportScheduleRunId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportScheduleId = table.Column<long>(type: "bigint", nullable: false),
                    RunUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    Recipients = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Outcome = table.Column<byte>(type: "tinyint", nullable: false),
                    Attempt = table.Column<byte>(type: "tinyint", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RPT_ScheduleRun", x => x.ReportScheduleRunId);
                    table.ForeignKey(
                        name: "FK_RPT_ScheduleRun_RPT_Schedule_ReportScheduleId",
                        column: x => x.ReportScheduleId,
                        principalTable: "RPT_Schedule",
                        principalColumn: "ReportScheduleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_DueDate_Status",
                table: "TRN_Invoice",
                columns: new[] { "DueDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_DailyMetric_Date_Location_Category",
                table: "RPT_DailyMetric",
                columns: new[] { "MetricDate", "LocationId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportExport_User",
                table: "RPT_Export",
                columns: new[] { "UserId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJob_Status",
                table: "RPT_ExportJob",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RPT_ExportJob_UserId",
                table: "RPT_ExportJob",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_ReportExportJob_Token",
                table: "RPT_ExportJob",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportPreference_Report",
                table: "RPT_Preference",
                column: "ReportKey");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedule_Due",
                table: "RPT_Schedule",
                columns: new[] { "IsActive", "NextRunUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RPT_Schedule_CreatedByUserId",
                table: "RPT_Schedule",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportScheduleRun_Schedule",
                table: "RPT_ScheduleRun",
                columns: new[] { "ReportScheduleId", "RunUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RPT_DailyMetric");

            migrationBuilder.DropTable(
                name: "RPT_Export");

            migrationBuilder.DropTable(
                name: "RPT_ExportJob");

            migrationBuilder.DropTable(
                name: "RPT_Favourite");

            migrationBuilder.DropTable(
                name: "RPT_Preference");

            migrationBuilder.DropTable(
                name: "RPT_ScheduleRun");

            migrationBuilder.DropTable(
                name: "RPT_Schedule");

            migrationBuilder.DropIndex(
                name: "IX_Invoice_DueDate_Status",
                table: "TRN_Invoice");
        }
    }
}
