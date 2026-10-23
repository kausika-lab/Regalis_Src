using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssetsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssetLocationId",
                table: "TRN_InventoryTransaction",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "IssueReason",
                table: "TRN_InventoryTransaction",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SourceId",
                table: "TRN_InventoryTransaction",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "SourceType",
                table: "TRN_InventoryTransaction",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssetCategoryId",
                table: "MST_InventoryItem",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandModel",
                table: "MST_InventoryItem",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KeptInLocationId",
                table: "MST_InventoryItem",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Kind",
                table: "MST_InventoryItem",
                type: "tinyint",
                nullable: false,
                // Every item that exists today is a consumable (or a service).
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "MST_InventoryItem",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "MST_InventoryItem",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VendorName",
                table: "MST_InventoryItem",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MST_AssetCategory",
                columns: table => new
                {
                    AssetCategoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DefaultKind = table.Column<byte>(type: "tinyint", nullable: false),
                    CodePrefix = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UsefulLifeYears = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_MST_AssetCategory", x => x.AssetCategoryId);
                });

            migrationBuilder.CreateTable(
                name: "MST_AssetLocation",
                columns: table => new
                {
                    AssetLocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocationName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkspaceId = table.Column<int>(type: "int", nullable: true),
                    IsStore = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_MST_AssetLocation", x => x.AssetLocationId);
                    table.ForeignKey(
                        name: "FK_MST_AssetLocation_MST_Workspace_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "MST_Workspace",
                        principalColumn: "WorkspaceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MST_CodeSequence",
                columns: table => new
                {
                    Prefix = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_CodeSequence", x => x.Prefix);
                });

            migrationBuilder.CreateTable(
                name: "TRN_ItemDocument",
                columns: table => new
                {
                    ItemDocumentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryItemId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<byte>(type: "tinyint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    UploadedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
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
                    table.PrimaryKey("PK_TRN_ItemDocument", x => x.ItemDocumentId);
                    table.ForeignKey(
                        name: "FK_TRN_ItemDocument_MST_InventoryItem_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "MST_InventoryItem",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_ItemEvent",
                columns: table => new
                {
                    ItemEventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryItemId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FromText = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ToText = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DocumentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    MetaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_ItemEvent", x => x.ItemEventId);
                    table.ForeignKey(
                        name: "FK_TRN_ItemEvent_MST_InventoryItem_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "MST_InventoryItem",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_GoodsIssue",
                columns: table => new
                {
                    GoodsIssueId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IssueNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<byte>(type: "tinyint", nullable: false),
                    FromLocationId = table.Column<int>(type: "int", nullable: true),
                    ToLocationId = table.Column<int>(type: "int", nullable: true),
                    RepairVendor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ExpectedCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreditNoteRaised = table.Column<bool>(type: "bit", nullable: true),
                    AmountRecovered = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ReportedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReasonText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PostedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    PostedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ReversesIssueId = table.Column<long>(type: "bigint", nullable: true),
                    ReversedByIssueId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_TRN_GoodsIssue", x => x.GoodsIssueId);
                    table.ForeignKey(
                        name: "FK_TRN_GoodsIssue_MST_AssetLocation_FromLocationId",
                        column: x => x.FromLocationId,
                        principalTable: "MST_AssetLocation",
                        principalColumn: "AssetLocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_GoodsIssue_MST_AssetLocation_ToLocationId",
                        column: x => x.ToLocationId,
                        principalTable: "MST_AssetLocation",
                        principalColumn: "AssetLocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_GoodsReceipt",
                columns: table => new
                {
                    GoodsReceiptId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    IsOpeningBalance = table.Column<bool>(type: "bit", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: true),
                    IntoLocationId = table.Column<int>(type: "int", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PostedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    PostedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ReversesReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    ReversedByReceiptId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_TRN_GoodsReceipt", x => x.GoodsReceiptId);
                    table.ForeignKey(
                        name: "FK_TRN_GoodsReceipt_MST_AssetLocation_IntoLocationId",
                        column: x => x.IntoLocationId,
                        principalTable: "MST_AssetLocation",
                        principalColumn: "AssetLocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_GoodsReceipt_MST_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "MST_Supplier",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_GoodsReceipt_TRN_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "TRN_PurchaseOrder",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_StockCount",
                columns: table => new
                {
                    StockCountId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CountDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AssetLocationId = table.Column<int>(type: "int", nullable: true),
                    CountedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubmittedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ConfirmedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ConfirmedByUserId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_TRN_StockCount", x => x.StockCountId);
                    table.ForeignKey(
                        name: "FK_TRN_StockCount_MST_AssetLocation_AssetLocationId",
                        column: x => x.AssetLocationId,
                        principalTable: "MST_AssetLocation",
                        principalColumn: "AssetLocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_AssetRepair",
                columns: table => new
                {
                    AssetRepairId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryItemId = table.Column<int>(type: "int", nullable: false),
                    GoodsIssueId = table.Column<long>(type: "bigint", nullable: false),
                    SentOn = table.Column<DateOnly>(type: "date", nullable: false),
                    RepairVendor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExpectedCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    ClosedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ClosedNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReturnStatus = table.Column<byte>(type: "tinyint", nullable: false),
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
                    table.PrimaryKey("PK_TRN_AssetRepair", x => x.AssetRepairId);
                    table.ForeignKey(
                        name: "FK_TRN_AssetRepair_MST_InventoryItem_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "MST_InventoryItem",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_AssetRepair_TRN_GoodsIssue_GoodsIssueId",
                        column: x => x.GoodsIssueId,
                        principalTable: "TRN_GoodsIssue",
                        principalColumn: "GoodsIssueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_GoodsIssueLine",
                columns: table => new
                {
                    GoodsIssueLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoodsIssueId = table.Column<long>(type: "bigint", nullable: false),
                    InventoryItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PreviousStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    PreviousLocationId = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    NewLocationId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_TRN_GoodsIssueLine", x => x.GoodsIssueLineId);
                    table.CheckConstraint("CK_GoodsIssueLine_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_TRN_GoodsIssueLine_MST_InventoryItem_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "MST_InventoryItem",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_GoodsIssueLine_TRN_GoodsIssue_GoodsIssueId",
                        column: x => x.GoodsIssueId,
                        principalTable: "TRN_GoodsIssue",
                        principalColumn: "GoodsIssueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_GoodsReceiptLine",
                columns: table => new
                {
                    GoodsReceiptLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoodsReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    InventoryItemId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PurchaseOrderLineId = table.Column<long>(type: "bigint", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AssetCategoryId = table.Column<int>(type: "int", nullable: true),
                    CodePrefix = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    BrandModel = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssetLocationId = table.Column<int>(type: "int", nullable: true),
                    Condition = table.Column<byte>(type: "tinyint", nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WarrantyUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    VendorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FirstCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    LastCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
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
                    table.PrimaryKey("PK_TRN_GoodsReceiptLine", x => x.GoodsReceiptLineId);
                    table.CheckConstraint("CK_GoodsReceiptLine_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_TRN_GoodsReceiptLine_MST_AssetCategory_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "MST_AssetCategory",
                        principalColumn: "AssetCategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_GoodsReceiptLine_MST_AssetLocation_AssetLocationId",
                        column: x => x.AssetLocationId,
                        principalTable: "MST_AssetLocation",
                        principalColumn: "AssetLocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_GoodsReceiptLine_MST_InventoryItem_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "MST_InventoryItem",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_GoodsReceiptLine_TRN_GoodsReceipt_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalTable: "TRN_GoodsReceipt",
                        principalColumn: "GoodsReceiptId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_GoodsReceiptLine_TRN_PurchaseOrderLine_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "TRN_PurchaseOrderLine",
                        principalColumn: "PurchaseOrderLineId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_StockCountLine",
                columns: table => new
                {
                    StockCountLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockCountId = table.Column<long>(type: "bigint", nullable: false),
                    InventoryItemId = table.Column<int>(type: "int", nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    RecordedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Difference = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
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
                    table.PrimaryKey("PK_TRN_StockCountLine", x => x.StockCountLineId);
                    table.ForeignKey(
                        name: "FK_TRN_StockCountLine_MST_InventoryItem_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "MST_InventoryItem",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_StockCountLine_TRN_StockCount_StockCountId",
                        column: x => x.StockCountId,
                        principalTable: "TRN_StockCount",
                        principalColumn: "StockCountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MST_AssetDetail",
                columns: table => new
                {
                    InventoryItemId = table.Column<int>(type: "int", nullable: false),
                    CodePrefix = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CodeNumber = table.Column<int>(type: "int", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssetLocationId = table.Column<int>(type: "int", nullable: false),
                    Condition = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WarrantyUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    LabelPrintedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    GoodsReceiptLineId = table.Column<long>(type: "bigint", nullable: true),
                    RetiredReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RetiredValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RetiredUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_AssetDetail", x => x.InventoryItemId);
                    table.ForeignKey(
                        name: "FK_MST_AssetDetail_MST_AssetLocation_AssetLocationId",
                        column: x => x.AssetLocationId,
                        principalTable: "MST_AssetLocation",
                        principalColumn: "AssetLocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MST_AssetDetail_MST_InventoryItem_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "MST_InventoryItem",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MST_AssetDetail_TRN_GoodsReceiptLine_GoodsReceiptLineId",
                        column: x => x.GoodsReceiptLineId,
                        principalTable: "TRN_GoodsReceiptLine",
                        principalColumn: "GoodsReceiptLineId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvTxn_Source",
                table: "TRN_InventoryTransaction",
                columns: new[] { "SourceType", "SourceId" },
                filter: "[SourceType] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_InventoryTransaction_AssetLocationId",
                table: "TRN_InventoryTransaction",
                column: "AssetLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItem_Kind_Category",
                table: "MST_InventoryItem",
                columns: new[] { "Kind", "AssetCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_MST_InventoryItem_AssetCategoryId",
                table: "MST_InventoryItem",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MST_InventoryItem_KeptInLocationId",
                table: "MST_InventoryItem",
                column: "KeptInLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_MST_InventoryItem_SupplierId",
                table: "MST_InventoryItem",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "UX_AssetCategory_Name",
                table: "MST_AssetCategory",
                column: "CategoryName",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDetail_Location_Status",
                table: "MST_AssetDetail",
                columns: new[] { "AssetLocationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetDetail_Serial",
                table: "MST_AssetDetail",
                column: "SerialNumber",
                filter: "[SerialNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MST_AssetDetail_GoodsReceiptLineId",
                table: "MST_AssetDetail",
                column: "GoodsReceiptLineId");

            migrationBuilder.CreateIndex(
                name: "UX_AssetDetail_Prefix_Number",
                table: "MST_AssetDetail",
                columns: new[] { "CodePrefix", "CodeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MST_AssetLocation_WorkspaceId",
                table: "MST_AssetLocation",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "UX_AssetLocation_Name",
                table: "MST_AssetLocation",
                column: "LocationName",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AssetRepair_Item_Open",
                table: "TRN_AssetRepair",
                columns: new[] { "InventoryItemId", "IsOpen" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_AssetRepair_GoodsIssueId",
                table: "TRN_AssetRepair",
                column: "GoodsIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssue_Date_Reason",
                table: "TRN_GoodsIssue",
                columns: new[] { "IssueDate", "Reason" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_GoodsIssue_FromLocationId",
                table: "TRN_GoodsIssue",
                column: "FromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_GoodsIssue_ToLocationId",
                table: "TRN_GoodsIssue",
                column: "ToLocationId");

            migrationBuilder.CreateIndex(
                name: "UX_GoodsIssue_Number",
                table: "TRN_GoodsIssue",
                column: "IssueNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssueLine_Item",
                table: "TRN_GoodsIssueLine",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_GoodsIssueLine_GoodsIssueId",
                table: "TRN_GoodsIssueLine",
                column: "GoodsIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipt_Date_Status",
                table: "TRN_GoodsReceipt",
                columns: new[] { "ReceiptDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_GoodsReceipt_IntoLocationId",
                table: "TRN_GoodsReceipt",
                column: "IntoLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_GoodsReceipt_PurchaseOrderId",
                table: "TRN_GoodsReceipt",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_GoodsReceipt_SupplierId",
                table: "TRN_GoodsReceipt",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "UX_GoodsReceipt_Number",
                table: "TRN_GoodsReceipt",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLine_Item",
                table: "TRN_GoodsReceiptLine",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_GoodsReceiptLine_AssetCategoryId",
                table: "TRN_GoodsReceiptLine",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_GoodsReceiptLine_AssetLocationId",
                table: "TRN_GoodsReceiptLine",
                column: "AssetLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_GoodsReceiptLine_GoodsReceiptId",
                table: "TRN_GoodsReceiptLine",
                column: "GoodsReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_GoodsReceiptLine_PurchaseOrderLineId",
                table: "TRN_GoodsReceiptLine",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemDocument_Item",
                table: "TRN_ItemDocument",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemEvent_Item",
                table: "TRN_ItemEvent",
                columns: new[] { "InventoryItemId", "ItemEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_StockCount_AssetLocationId",
                table: "TRN_StockCount",
                column: "AssetLocationId");

            migrationBuilder.CreateIndex(
                name: "UX_StockCount_Number",
                table: "TRN_StockCount",
                column: "CountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_StockCountLine_InventoryItemId",
                table: "TRN_StockCountLine",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "UX_StockCountLine_Count_Item",
                table: "TRN_StockCountLine",
                columns: new[] { "StockCountId", "InventoryItemId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MST_InventoryItem_MST_AssetCategory_AssetCategoryId",
                table: "MST_InventoryItem",
                column: "AssetCategoryId",
                principalTable: "MST_AssetCategory",
                principalColumn: "AssetCategoryId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MST_InventoryItem_MST_AssetLocation_KeptInLocationId",
                table: "MST_InventoryItem",
                column: "KeptInLocationId",
                principalTable: "MST_AssetLocation",
                principalColumn: "AssetLocationId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MST_InventoryItem_MST_Supplier_SupplierId",
                table: "MST_InventoryItem",
                column: "SupplierId",
                principalTable: "MST_Supplier",
                principalColumn: "SupplierId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TRN_InventoryTransaction_MST_AssetLocation_AssetLocationId",
                table: "TRN_InventoryTransaction",
                column: "AssetLocationId",
                principalTable: "MST_AssetLocation",
                principalColumn: "AssetLocationId",
                onDelete: ReferentialAction.Restrict);

            // ── Data (Assets spec, Oct 2026). Keep this SQL ASCII-only: sqlcmd reads the
            // script in the ANSI code page. Fresh databases (tests) get the categories and
            // rooms here too; permissions, settings and menus there come from the seeder.

            // §3.4 categories: name, default kind (1 consumable, 2 asset), code prefix,
            // useful life (open item 3 - suggested figures, editable).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [MST_AssetCategory])
INSERT INTO [MST_AssetCategory] ([CategoryName], [DefaultKind], [CodePrefix], [UsefulLifeYears], [DisplayOrder], [CreatedDate], [IsActive], [IsDeleted])
VALUES (N'Office Interior', 2, N'R-2W-E-I', 10, 1, SYSUTCDATETIME(), 1, 0),
       (N'Electrical / Appliance', 2, N'R-2W-E-E', 7, 2, SYSUTCDATETIME(), 1, 0),
       (N'Furniture', 2, N'R-2W-E-F', 10, 3, SYSUTCDATETIME(), 1, 0),
       (N'IT Equipment', 2, N'R-2W-E-IT', 4, 4, SYSUTCDATETIME(), 1, 0),
       (N'Pantry', 1, N'PAN', NULL, 5, SYSUTCDATETIME(), 1, 0),
       (N'Housekeeping', 1, N'HK', NULL, 6, SYSUTCDATETIME(), 1, 0),
       (N'Stationery', 1, N'ST', NULL, 7, SYSUTCDATETIME(), 1, 0);");

            // §3.5 rooms, as the team's asset list names them, plus the Store.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [MST_AssetLocation])
INSERT INTO [MST_AssetLocation] ([LocationName], [IsStore], [DisplayOrder], [CreatedDate], [IsActive], [IsDeleted])
SELECT v.[Name], v.[IsStore], v.[Ord], SYSUTCDATETIME(), 1, 0
FROM (VALUES (N'Store', 1, 1), (N'MD Cabin', 0, 2), (N'Meeting Room', 0, 3), (N'Conference Room', 0, 4),
             (N'Private Cabin 1', 0, 5), (N'Private Cabin 2', 0, 6), (N'Private Cabin 3', 0, 7),
             (N'Private Workstation', 0, 8), (N'Open Work Station', 0, 9), (N'Work Station', 0, 10),
             (N'8 Seater Cabin', 0, 11), (N'Reception Area', 0, 12), (N'Server Room', 0, 13),
             (N'Pantry Area', 0, 14), (N'Rest Room', 0, 15), (N'Rest Area', 0, 16), (N'Walking Space', 0, 17),
             (N'Walking Workspace', 0, 18), (N'Custom Space', 0, 19), (N'Amstag Area', 0, 20), (N'Entrance', 0, 21)
     ) v([Name], [IsStore], [Ord]);");

            // Open item 4: a room that IS a workspace links to it ("Private Cabin 1" is the
            // workspace "Private Cabin 01"), so either side can answer what is in it.
            migrationBuilder.Sql(@"
UPDATE l SET [WorkspaceId] = w.[WorkspaceId]
FROM [MST_AssetLocation] l
CROSS APPLY (SELECT TOP 1 ws.[WorkspaceId] FROM [MST_Workspace] ws
             WHERE ws.[IsDeleted] = 0
               AND (ws.[WorkspaceName] = l.[LocationName] OR REPLACE(ws.[WorkspaceName], N' 0', N' ') = l.[LocationName])
             ORDER BY ws.[WorkspaceId]) w
WHERE l.[WorkspaceId] IS NULL;");

            // Existing items: the free-text category becomes a category row where the names
            // match, and every stocked consumable gets a room it is kept in.
            migrationBuilder.Sql(@"
UPDATE i SET [AssetCategoryId] = c.[AssetCategoryId], [Category] = c.[CategoryName]
FROM [MST_InventoryItem] i
JOIN [MST_AssetCategory] c ON c.[CategoryName] = LTRIM(RTRIM(i.[Category]))
WHERE i.[AssetCategoryId] IS NULL;

UPDATE i SET [KeptInLocationId] = COALESCE(
        CASE WHEN c.[CategoryName] = N'Pantry' THEN (SELECT TOP 1 [AssetLocationId] FROM [MST_AssetLocation] WHERE [LocationName] = N'Pantry Area') END,
        (SELECT TOP 1 [AssetLocationId] FROM [MST_AssetLocation] WHERE [IsStore] = 1 ORDER BY [DisplayOrder]))
FROM [MST_InventoryItem] i
LEFT JOIN [MST_AssetCategory] c ON c.[AssetCategoryId] = i.[AssetCategoryId]
WHERE i.[KeptInLocationId] IS NULL AND i.[ItemType] = 1;");

            // §5.2: there is no opening quantity any more - even the first number has a
            // movement behind it. Opening quantities typed on the old item form become one
            // posted opening-balance receipt, one ledger row per item, dated when the item
            // was created; the column is then zeroed. Stock figures do not change: checked.
            migrationBuilder.Sql(@"
DECLARE @admin bigint = (SELECT TOP 1 [UserId] FROM [MST_User] WHERE [UserType] = 2 AND [IsDeleted] = 0 ORDER BY [UserId]);
IF @admin IS NOT NULL
   AND EXISTS (SELECT 1 FROM [MST_InventoryItem] WHERE [IsDeleted] = 0 AND [OpeningQuantity] > 0)
   AND EXISTS (SELECT 1 FROM [MST_NumberSequence] WHERE [SequenceKey] = N'InventoryTransaction')
BEGIN
    DECLARE @before TABLE ([ItemId] int PRIMARY KEY, [Stock] decimal(18,3));
    INSERT INTO @before
    SELECT i.[InventoryItemId], i.[OpeningQuantity] + ISNULL((SELECT SUM(CASE WHEN t.[TransactionType] IN (2, 3) THEN -t.[Quantity] ELSE t.[Quantity] END)
                                                            FROM [TRN_InventoryTransaction] t WHERE t.[InventoryItemId] = i.[InventoryItemId] AND t.[IsDeleted] = 0), 0)
    FROM [MST_InventoryItem] i WHERE i.[IsDeleted] = 0;

    DECLARE @items TABLE ([ItemId] int PRIMARY KEY, [Qty] decimal(18,3), [Cost] decimal(18,2), [Day] date, [Branch] int, [Room] int, [Rn] int);
    INSERT INTO @items
    SELECT i.[InventoryItemId], i.[OpeningQuantity], i.[UnitCost],
           CASE WHEN f.[FirstDay] IS NOT NULL AND f.[FirstDay] < CAST(DATEADD(MINUTE, 330, i.[CreatedDate]) AS date)
                THEN f.[FirstDay] ELSE CAST(DATEADD(MINUTE, 330, i.[CreatedDate]) AS date) END,
           i.[LocationId], i.[KeptInLocationId],
           ROW_NUMBER() OVER (ORDER BY i.[InventoryItemId])
    FROM [MST_InventoryItem] i
    OUTER APPLY (SELECT MIN(t.[TransactionDate]) AS [FirstDay] FROM [TRN_InventoryTransaction] t WHERE t.[InventoryItemId] = i.[InventoryItemId]) f
    WHERE i.[IsDeleted] = 0 AND i.[OpeningQuantity] > 0;

    DECLARE @day date = (SELECT MIN([Day]) FROM @items);
    DECLARE @prefix nvarchar(40) = N'IN-' + CAST(YEAR(@day) AS nvarchar(4));
    IF NOT EXISTS (SELECT 1 FROM [MST_CodeSequence] WHERE [Prefix] = @prefix)
        INSERT INTO [MST_CodeSequence] ([Prefix], [LastNumber]) VALUES (@prefix, 0);
    UPDATE [MST_CodeSequence] SET [LastNumber] = [LastNumber] + 1 WHERE [Prefix] = @prefix;
    DECLARE @number nvarchar(30) = @prefix + N'-' + RIGHT(N'0000' + CAST((SELECT [LastNumber] FROM [MST_CodeSequence] WHERE [Prefix] = @prefix) AS nvarchar(10)), 4);

    INSERT INTO [TRN_GoodsReceipt] ([ReceiptNumber], [ReceiptDate], [Status], [Source], [IsOpeningBalance], [TotalAmount], [Notes],
                                    [PostedByUserId], [PostedUtc], [CreatedBy], [CreatedDate], [IsActive], [IsDeleted])
    VALUES (@number, @day, 2, 6, 1, (SELECT ISNULL(SUM(ROUND([Qty] * ISNULL([Cost], 0), 2)), 0) FROM @items),
            N'Opening quantities from the old item form, moved onto the ledger', @admin, SYSUTCDATETIME(), @admin, SYSUTCDATETIME(), 1, 0);
    DECLARE @receipt bigint = SCOPE_IDENTITY();

    INSERT INTO [TRN_GoodsReceiptLine] ([GoodsReceiptId], [LineNumber], [Kind], [InventoryItemId], [Quantity], [Rate], [Amount],
                                        [CreatedBy], [CreatedDate], [IsActive], [IsDeleted])
    SELECT @receipt, [Rn], 1, [ItemId], [Qty], [Cost], ROUND([Qty] * ISNULL([Cost], 0), 2), @admin, SYSUTCDATETIME(), 1, 0 FROM @items;

    DECLARE @seq bigint = (SELECT [CurrentValue] FROM [MST_NumberSequence] WHERE [SequenceKey] = N'InventoryTransaction');
    DECLARE @itx nvarchar(10) = (SELECT [Prefix] FROM [MST_NumberSequence] WHERE [SequenceKey] = N'InventoryTransaction');
    INSERT INTO [TRN_InventoryTransaction] ([InventoryItemId], [TransactionNumber], [TransactionType], [Quantity], [TransactionDate],
            [SourceOrDestination], [ReferenceNumber], [UnitCost], [LocationId], [EnteredByUserId], [Notes],
            [SourceType], [SourceId], [AssetLocationId], [CreatedBy], [CreatedDate], [IsActive], [IsDeleted])
    SELECT [ItemId], @itx + RIGHT(N'000000' + CAST(@seq + [Rn] AS nvarchar(20)), 6), 4, [Qty], [Day],
           N'Opening balance', @number, [Cost], [Branch], @admin, N'Opening quantity moved onto the ledger',
           1, @receipt, [Room], @admin, SYSUTCDATETIME(), 1, 0
    FROM @items;
    UPDATE [MST_NumberSequence] SET [CurrentValue] = @seq + (SELECT COUNT(*) FROM @items) WHERE [SequenceKey] = N'InventoryTransaction';

    INSERT INTO [TRN_ItemEvent] ([InventoryItemId], [EventType], [EventDate], [Summary], [FromText], [DocumentNumber], [ActorUserId], [CreatedUtc])
    SELECT [ItemId], N'RECEIVED', [Day], N'Opening balance (moved from the old opening quantity)', @number, @number, @admin, SYSUTCDATETIME() FROM @items;

    UPDATE i SET [OpeningQuantity] = 0 FROM [MST_InventoryItem] i JOIN @items x ON x.[ItemId] = i.[InventoryItemId];

    IF EXISTS (
        SELECT 1 FROM @before b
        JOIN [MST_InventoryItem] i ON i.[InventoryItemId] = b.[ItemId]
        WHERE b.[Stock] <> i.[OpeningQuantity] + ISNULL((SELECT SUM(CASE WHEN t.[TransactionType] IN (2, 3) THEN -t.[Quantity] ELSE t.[Quantity] END)
                                                       FROM [TRN_InventoryTransaction] t WHERE t.[InventoryItemId] = b.[ItemId] AND t.[IsDeleted] = 0), 0))
        THROW 50101, N'AssetsModule: moving opening quantities onto the ledger changed a stock figure.', 1;
END;");

            // Existing databases only (a fresh one is seeded at start-up): the Assets
            // permissions (spec §13), its settings and the routes the sidebar links to.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [MST_Permission])
BEGIN
    DECLARE @base int = (SELECT ISNULL(MAX([DisplayOrder]), 0) FROM [MST_Permission]);
    INSERT INTO [MST_Permission] ([PermissionCode], [ModuleName], [ActionName], [Description], [DisplayOrder], [CreatedDate], [IsActive], [IsDeleted])
    SELECT v.[Code], N'INVENTORY', v.[Action], v.[Action] + N' on INVENTORY', @base + v.[Ord], SYSUTCDATETIME(), 1, 0
    FROM (VALUES (N'INVENTORY.RECEIVE', N'RECEIVE', 1), (N'INVENTORY.ISSUE', N'ISSUE', 2), (N'INVENTORY.LABELS', N'LABELS', 3),
                 (N'INVENTORY.COUNT', N'COUNT', 4), (N'INVENTORY.REVERSE', N'REVERSE', 5)) v([Code], [Action], [Ord])
    WHERE NOT EXISTS (SELECT 1 FROM [MST_Permission] p WHERE p.[PermissionCode] = v.[Code]);

    -- A role that may add items today may also receive, issue, print labels and count.
    INSERT INTO [MAP_RolePermission] ([RoleId], [PermissionId])
    SELECT DISTINCT rp.[RoleId], np.[PermissionId]
    FROM [MAP_RolePermission] rp
    JOIN [MST_Permission] cp ON cp.[PermissionId] = rp.[PermissionId] AND cp.[PermissionCode] = N'INVENTORY.CREATE'
    JOIN [MST_Permission] np ON np.[PermissionCode] IN (N'INVENTORY.RECEIVE', N'INVENTORY.ISSUE', N'INVENTORY.LABELS', N'INVENTORY.COUNT')
    JOIN [MST_Role] r ON r.[RoleId] = rp.[RoleId] AND r.[RoleCode] <> N'CUSTOMER'
    WHERE NOT EXISTS (SELECT 1 FROM [MAP_RolePermission] x WHERE x.[RoleId] = rp.[RoleId] AND x.[PermissionId] = np.[PermissionId]);

    -- Reversing changes history: the administrator only.
    INSERT INTO [MAP_RolePermission] ([RoleId], [PermissionId])
    SELECT r.[RoleId], p.[PermissionId]
    FROM [MST_Role] r JOIN [MST_Permission] p ON p.[PermissionCode] = N'INVENTORY.REVERSE'
    WHERE r.[RoleCode] = N'ADMIN'
      AND NOT EXISTS (SELECT 1 FROM [MAP_RolePermission] x WHERE x.[RoleId] = r.[RoleId] AND x.[PermissionId] = p.[PermissionId]);
END;

IF EXISTS (SELECT 1 FROM [MST_AppSetting])
INSERT INTO [MST_AppSetting] ([SettingKey], [SettingValue], [SettingGroup], [DataType], [Description], [IsEditableByAdmin], [IsSecret], [CreatedDate], [IsActive], [IsDeleted])
SELECT v.[K], v.[V], N'Assets', v.[T], v.[D], 1, 0, SYSUTCDATETIME(), 1, 0
FROM (VALUES (N'Assets.LabelContactEmail', N'admin@regalis.in', N'string', N'QR labels: If found, contact this address'),
             (N'Assets.LabelOwnerName', N'Regalis Workspace', N'string', N'QR labels: Property of this name'),
             (N'Assets.WarrantyAlertDays', N'60', N'int', N'Days before Warranty until that an asset is flagged Expiring soon'),
             (N'Assets.Units', N'kg,litre,piece,box,bottle,ream,packet,Nos', N'string', N'Units the consumable form offers, comma-separated')
     ) v([K], [V], [T], [D])
WHERE NOT EXISTS (SELECT 1 FROM [MST_AppSetting] s WHERE s.[SettingKey] = v.[K]);

UPDATE [MST_Menu] SET [Route] = N'/admin/assets/items' WHERE [MenuCode] IN (N'ADM_INVENTORY', N'ADM_INVENTORY_ITEMS') AND [Route] = N'/admin/inventory/items';
UPDATE [MST_Menu] SET [Route] = N'/admin/assets/incoming' WHERE [MenuCode] = N'ADM_INVENTORY_INCOMING' AND [Route] = N'/admin/inventory/incoming';
UPDATE [MST_Menu] SET [Route] = N'/admin/assets/outgoing' WHERE [MenuCode] = N'ADM_INVENTORY_OUTGOING' AND [Route] = N'/admin/inventory/outgoing';
UPDATE [MST_Menu] SET [Route] = N'/admin/assets/stock' WHERE [MenuCode] = N'ADM_INVENTORY_STOCK' AND [Route] = N'/admin/inventory/stock';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MST_InventoryItem_MST_AssetCategory_AssetCategoryId",
                table: "MST_InventoryItem");

            migrationBuilder.DropForeignKey(
                name: "FK_MST_InventoryItem_MST_AssetLocation_KeptInLocationId",
                table: "MST_InventoryItem");

            migrationBuilder.DropForeignKey(
                name: "FK_MST_InventoryItem_MST_Supplier_SupplierId",
                table: "MST_InventoryItem");

            migrationBuilder.DropForeignKey(
                name: "FK_TRN_InventoryTransaction_MST_AssetLocation_AssetLocationId",
                table: "TRN_InventoryTransaction");

            migrationBuilder.DropTable(
                name: "MST_AssetDetail");

            migrationBuilder.DropTable(
                name: "MST_CodeSequence");

            migrationBuilder.DropTable(
                name: "TRN_AssetRepair");

            migrationBuilder.DropTable(
                name: "TRN_GoodsIssueLine");

            migrationBuilder.DropTable(
                name: "TRN_ItemDocument");

            migrationBuilder.DropTable(
                name: "TRN_ItemEvent");

            migrationBuilder.DropTable(
                name: "TRN_StockCountLine");

            migrationBuilder.DropTable(
                name: "TRN_GoodsReceiptLine");

            migrationBuilder.DropTable(
                name: "TRN_GoodsIssue");

            migrationBuilder.DropTable(
                name: "TRN_StockCount");

            migrationBuilder.DropTable(
                name: "MST_AssetCategory");

            migrationBuilder.DropTable(
                name: "TRN_GoodsReceipt");

            migrationBuilder.DropTable(
                name: "MST_AssetLocation");

            migrationBuilder.DropIndex(
                name: "IX_InvTxn_Source",
                table: "TRN_InventoryTransaction");

            migrationBuilder.DropIndex(
                name: "IX_TRN_InventoryTransaction_AssetLocationId",
                table: "TRN_InventoryTransaction");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItem_Kind_Category",
                table: "MST_InventoryItem");

            migrationBuilder.DropIndex(
                name: "IX_MST_InventoryItem_AssetCategoryId",
                table: "MST_InventoryItem");

            migrationBuilder.DropIndex(
                name: "IX_MST_InventoryItem_KeptInLocationId",
                table: "MST_InventoryItem");

            migrationBuilder.DropIndex(
                name: "IX_MST_InventoryItem_SupplierId",
                table: "MST_InventoryItem");

            migrationBuilder.DropColumn(
                name: "AssetLocationId",
                table: "TRN_InventoryTransaction");

            migrationBuilder.DropColumn(
                name: "IssueReason",
                table: "TRN_InventoryTransaction");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "TRN_InventoryTransaction");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "TRN_InventoryTransaction");

            migrationBuilder.DropColumn(
                name: "AssetCategoryId",
                table: "MST_InventoryItem");

            migrationBuilder.DropColumn(
                name: "BrandModel",
                table: "MST_InventoryItem");

            migrationBuilder.DropColumn(
                name: "KeptInLocationId",
                table: "MST_InventoryItem");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "MST_InventoryItem");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "MST_InventoryItem");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "MST_InventoryItem");

            migrationBuilder.DropColumn(
                name: "VendorName",
                table: "MST_InventoryItem");
        }
    }
}
