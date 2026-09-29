using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountCategory",
                table: "TRN_PurchaseOrderLine",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CgstAmount",
                table: "TRN_PurchaseOrderLine",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IgstAmount",
                table: "TRN_PurchaseOrderLine",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SgstAmount",
                table: "TRN_PurchaseOrderLine",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AdjustmentAmount",
                table: "TRN_PurchaseOrder",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AdjustmentLabel",
                table: "TRN_PurchaseOrder",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ApprovedByUserId",
                table: "TRN_PurchaseOrder",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedUtc",
                table: "TRN_PurchaseOrder",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CgstAmount",
                table: "TRN_PurchaseOrder",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddress",
                table: "TRN_PurchaseOrder",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "DiscountType",
                table: "TRN_PurchaseOrder",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "TRN_PurchaseOrder",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IgstAmount",
                table: "TRN_PurchaseOrder",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsInterState",
                table: "TRN_PurchaseOrder",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReverseCharge",
                table: "TRN_PurchaseOrder",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "TRN_PurchaseOrder",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SgstAmount",
                table: "TRN_PurchaseOrder",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ShipmentPreference",
                table: "TRN_PurchaseOrder",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedUtc",
                table: "TRN_PurchaseOrder",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TdsAmount",
                table: "TRN_PurchaseOrder",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TdsCode",
                table: "TRN_PurchaseOrder",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TdsLabel",
                table: "TRN_PurchaseOrder",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TdsPercent",
                table: "TRN_PurchaseOrder",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TermsAndConditions",
                table: "TRN_PurchaseOrder",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TRN_PurchaseOrderAttachment",
                columns: table => new
                {
                    PurchaseOrderAttachmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    UploadedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_PurchaseOrderAttachment", x => x.PurchaseOrderAttachmentId);
                    table.ForeignKey(
                        name: "FK_TRN_PurchaseOrderAttachment_TRN_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "TRN_PurchaseOrder",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_PurchaseOrderStatusHistory",
                columns: table => new
                {
                    PurchaseOrderStatusHistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: false),
                    FromStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    ToStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ChangedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ChangedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_PurchaseOrderStatusHistory", x => x.PurchaseOrderStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_TRN_PurchaseOrderStatusHistory_TRN_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "TRN_PurchaseOrder",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderAttachment_PurchaseOrder",
                table: "TRN_PurchaseOrderAttachment",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderStatusHistory_PurchaseOrder",
                table: "TRN_PurchaseOrderStatusHistory",
                column: "PurchaseOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TRN_PurchaseOrderAttachment");

            migrationBuilder.DropTable(
                name: "TRN_PurchaseOrderStatusHistory");

            migrationBuilder.DropColumn(
                name: "AccountCategory",
                table: "TRN_PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "CgstAmount",
                table: "TRN_PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "IgstAmount",
                table: "TRN_PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "SgstAmount",
                table: "TRN_PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "AdjustmentAmount",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "AdjustmentLabel",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "ApprovedUtc",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "CgstAmount",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "DeliveryAddress",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "IgstAmount",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "IsInterState",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "IsReverseCharge",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "SgstAmount",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "ShipmentPreference",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "SubmittedUtc",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "TdsAmount",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "TdsCode",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "TdsLabel",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "TdsPercent",
                table: "TRN_PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "TermsAndConditions",
                table: "TRN_PurchaseOrder");
        }
    }
}
