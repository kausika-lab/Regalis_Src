using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationBillingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CgstAmount",
                table: "TRN_QuotationLine",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountShare",
                table: "TRN_QuotationLine",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndUtc",
                table: "TRN_QuotationLine",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IgstAmount",
                table: "TRN_QuotationLine",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "LineNotes",
                table: "TRN_QuotationLine",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "LineType",
                table: "TRN_QuotationLine",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<int>(
                name: "PassPlanId",
                table: "TRN_QuotationLine",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "ServiceType",
                table: "TRN_QuotationLine",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<decimal>(
                name: "SgstAmount",
                table: "TRN_QuotationLine",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "StandardRate",
                table: "TRN_QuotationLine",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartUtc",
                table: "TRN_QuotationLine",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxableAmount",
                table: "TRN_QuotationLine",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "WorkspaceId",
                table: "TRN_QuotationLine",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdjustmentAmount",
                table: "TRN_Quotation",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AdjustmentLabel",
                table: "TRN_Quotation",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdvanceInvoiceEnabled",
                table: "TRN_Quotation",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte>(
                name: "AdvanceType",
                table: "TRN_Quotation",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<decimal>(
                name: "AdvanceValue",
                table: "TRN_Quotation",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CustomerCompanyName",
                table: "TRN_Quotation",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionConfirmedBy",
                table: "TRN_Quotation",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionIpAddress",
                table: "TRN_Quotation",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "DecisionMode",
                table: "TRN_Quotation",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionNote",
                table: "TRN_Quotation",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DecisionOnBehalf",
                table: "TRN_Quotation",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte>(
                name: "DiscountType",
                table: "TRN_Quotation",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "TRN_Quotation",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "InternalNote",
                table: "TRN_Quotation",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaceOfSupplyStateCode",
                table: "TRN_Quotation",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "TRN_Quotation",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionCode",
                table: "TRN_Quotation",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                table: "TRN_Quotation",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "RevisionOfQuotationId",
                table: "TRN_Quotation",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RoundOffAmount",
                table: "TRN_Quotation",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "SalespersonId",
                table: "TRN_Quotation",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "TRN_Quotation",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "TaxMode",
                table: "TRN_Quotation",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<decimal>(
                name: "TdsAmount",
                table: "TRN_Quotation",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TdsCode",
                table: "TRN_Quotation",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TdsLabel",
                table: "TRN_Quotation",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TdsPercent",
                table: "TRN_Quotation",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AdjustmentAmount",
                table: "TRN_Invoice",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AdjustmentLabel",
                table: "TRN_Invoice",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RoundOffAmount",
                table: "TRN_Invoice",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TdsAmount",
                table: "TRN_Invoice",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TdsLabel",
                table: "TRN_Invoice",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TdsPercent",
                table: "TRN_Invoice",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerMonth",
                table: "MST_Workspace",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TRN_QuotationAttachment",
                columns: table => new
                {
                    QuotationAttachmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SendWithEmail = table.Column<bool>(type: "bit", nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    UploadedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_QuotationAttachment", x => x.QuotationAttachmentId);
                    table.ForeignKey(
                        name: "FK_TRN_QuotationAttachment_TRN_Quotation_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "TRN_Quotation",
                        principalColumn: "QuotationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_QuotationEvent",
                columns: table => new
                {
                    QuotationEventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationId = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<byte>(type: "tinyint", nullable: false),
                    ActorType = table.Column<byte>(type: "tinyint", nullable: false),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DeliveryStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_QuotationEvent", x => x.QuotationEventId);
                    table.ForeignKey(
                        name: "FK_TRN_QuotationEvent_TRN_Quotation_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "TRN_Quotation",
                        principalColumn: "QuotationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quotation_RevisionOf",
                table: "TRN_Quotation",
                column: "RevisionOfQuotationId",
                filter: "[RevisionOfQuotationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationAttachment_Quotation",
                table: "TRN_QuotationAttachment",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationEvent_Quotation_Created",
                table: "TRN_QuotationEvent",
                columns: new[] { "QuotationId", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TRN_QuotationAttachment");

            migrationBuilder.DropTable(
                name: "TRN_QuotationEvent");

            migrationBuilder.DropIndex(
                name: "IX_Quotation_RevisionOf",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "CgstAmount",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "DiscountShare",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "EndUtc",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "IgstAmount",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "LineNotes",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "LineType",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "PassPlanId",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "ServiceType",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "SgstAmount",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "StandardRate",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "StartUtc",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "TaxableAmount",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "AdjustmentAmount",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "AdjustmentLabel",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "AdvanceInvoiceEnabled",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "AdvanceType",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "AdvanceValue",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "CustomerCompanyName",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "DecisionConfirmedBy",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "DecisionIpAddress",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "DecisionMode",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "DecisionNote",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "DecisionOnBehalf",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "InternalNote",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "PlaceOfSupplyStateCode",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "RejectionCode",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "RevisionOfQuotationId",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "RoundOffAmount",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "SalespersonId",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "TaxMode",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "TdsAmount",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "TdsCode",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "TdsLabel",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "TdsPercent",
                table: "TRN_Quotation");

            migrationBuilder.DropColumn(
                name: "AdjustmentAmount",
                table: "TRN_Invoice");

            migrationBuilder.DropColumn(
                name: "AdjustmentLabel",
                table: "TRN_Invoice");

            migrationBuilder.DropColumn(
                name: "RoundOffAmount",
                table: "TRN_Invoice");

            migrationBuilder.DropColumn(
                name: "TdsAmount",
                table: "TRN_Invoice");

            migrationBuilder.DropColumn(
                name: "TdsLabel",
                table: "TRN_Invoice");

            migrationBuilder.DropColumn(
                name: "TdsPercent",
                table: "TRN_Invoice");

            migrationBuilder.DropColumn(
                name: "PricePerMonth",
                table: "MST_Workspace");
        }
    }
}
