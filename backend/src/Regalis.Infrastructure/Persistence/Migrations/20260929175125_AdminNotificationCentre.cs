using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminNotificationCentre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MST_AdminNotificationSetting",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Email = table.Column<bool>(type: "bit", nullable: false),
                    WhatsApp = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_AdminNotificationSetting", x => new { x.UserId, x.Type });
                    table.ForeignKey(
                        name: "FK_MST_AdminNotificationSetting_MST_User_UserId",
                        column: x => x.UserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_AdminNotification",
                columns: table => new
                {
                    AdminNotificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    EventCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Severity = table.Column<byte>(type: "tinyint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    ReferenceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ReferenceId = table.Column<long>(type: "bigint", nullable: true),
                    ReferenceLabel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ActionLabel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ActionRoute = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    DeliveredUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DeliveryNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_AdminNotification", x => x.AdminNotificationId);
                    table.ForeignKey(
                        name: "FK_TRN_AdminNotification_MST_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Announcement",
                columns: table => new
                {
                    AnnouncementId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Audience = table.Column<byte>(type: "tinyint", nullable: false),
                    TargetCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    SendEmail = table.Column<bool>(type: "bit", nullable: false),
                    SendWhatsApp = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ScheduledForUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    SentUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    SentByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RecipientCount = table.Column<int>(type: "int", nullable: false),
                    EmailSentCount = table.Column<int>(type: "int", nullable: false),
                    WhatsAppSentCount = table.Column<int>(type: "int", nullable: false),
                    WhatsAppManualShare = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TRN_Announcement", x => x.AnnouncementId);
                    table.ForeignKey(
                        name: "FK_TRN_Announcement_MST_User_TargetCustomerId",
                        column: x => x.TargetCustomerId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_AdminNotificationRead",
                columns: table => new
                {
                    AdminNotificationId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ReadUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_AdminNotificationRead", x => new { x.AdminNotificationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TRN_AdminNotificationRead_MST_User_UserId",
                        column: x => x.UserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_AdminNotificationRead_TRN_AdminNotification_AdminNotificationId",
                        column: x => x.AdminNotificationId,
                        principalTable: "TRN_AdminNotification",
                        principalColumn: "AdminNotificationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotification_Created",
                table: "TRN_AdminNotification",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotification_Reference",
                table: "TRN_AdminNotification",
                columns: new[] { "ReferenceType", "ReferenceId", "EventCode" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotification_Type_Created",
                table: "TRN_AdminNotification",
                columns: new[] { "Type", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotification_Undelivered",
                table: "TRN_AdminNotification",
                column: "DeliveredUtc",
                filter: "[DeliveredUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_AdminNotification_CustomerId",
                table: "TRN_AdminNotification",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotificationRead_User",
                table: "TRN_AdminNotificationRead",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcement_Status_Scheduled",
                table: "TRN_Announcement",
                columns: new[] { "Status", "ScheduledForUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Announcement_TargetCustomerId",
                table: "TRN_Announcement",
                column: "TargetCustomerId");

            // Until now each admin alert was copied into TRN_Notification once per admin
            // (Audience = 3 AllAdmins, with that admin's UserId). Fold each set of copies
            // into one TRN_AdminNotification row and keep every admin's read state. The old
            // copies are left in place — nothing is deleted. Marked delivered so the new
            // worker never emails about them.
            migrationBuilder.Sql("""
                INSERT INTO TRN_AdminNotification
                    (Type, EventCode, Severity, Title, Body, CustomerId, ReferenceType, ReferenceId,
                     ReferenceLabel, ActionLabel, ActionRoute, CreatedUtc, DeliveredUtc, DeliveryNote)
                SELECT CASE n.Category WHEN 1 THEN 2 WHEN 4 THEN 3 WHEN 8 THEN 5 WHEN 12 THEN 7 ELSE 6 END,
                       'legacy', 1, LEFT(n.Title, 200), LEFT(n.Message, 1000), NULL, n.ReferenceType,
                       n.ReferenceId, NULL, n.ActionLabel, n.ActionRoute,
                       MIN(n.CreatedDate), MIN(n.CreatedDate), 'Moved from the old per-admin feed'
                FROM TRN_Notification n
                WHERE n.Audience = 3 AND n.UserId IS NOT NULL AND n.IsDeleted = 0
                GROUP BY n.Category, n.Title, n.Message, n.ReferenceType, n.ReferenceId,
                         n.ActionLabel, n.ActionRoute, n.SentUtc;

                INSERT INTO TRN_AdminNotificationRead (AdminNotificationId, UserId, ReadUtc)
                SELECT a.AdminNotificationId, n.UserId, MIN(ISNULL(n.ReadUtc, n.CreatedDate))
                FROM TRN_Notification n
                JOIN TRN_AdminNotification a
                  ON a.EventCode = 'legacy'
                 AND a.Title = LEFT(n.Title, 200) AND a.Body = LEFT(n.Message, 1000)
                 AND ISNULL(a.ReferenceType, '') = ISNULL(n.ReferenceType, '')
                 AND ISNULL(a.ReferenceId, -1) = ISNULL(n.ReferenceId, -1)
                JOIN MST_User u ON u.UserId = n.UserId
                WHERE n.Audience = 3 AND n.UserId IS NOT NULL AND n.IsDeleted = 0 AND n.IsRead = 1
                GROUP BY a.AdminNotificationId, n.UserId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MST_AdminNotificationSetting");

            migrationBuilder.DropTable(
                name: "TRN_AdminNotificationRead");

            migrationBuilder.DropTable(
                name: "TRN_Announcement");

            migrationBuilder.DropTable(
                name: "TRN_AdminNotification");
        }
    }
}
