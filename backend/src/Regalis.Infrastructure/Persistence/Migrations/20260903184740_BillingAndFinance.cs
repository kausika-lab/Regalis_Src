using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillingAndFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "QuotationId",
                table: "TRN_Invoice",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsAndConditions",
                table: "TRN_Invoice",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentPath",
                table: "TRN_InventoryTransaction",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseInvoiceNumber",
                table: "TRN_InventoryTransaction",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PurchaseOrderId",
                table: "TRN_InventoryTransaction",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "TRN_InventoryTransaction",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MST_BankAccount",
                columns: table => new
                {
                    BankAccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountNumberMasked = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Ifsc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Branch = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OpeningBalanceAsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_MST_BankAccount", x => x.BankAccountId);
                    table.ForeignKey(
                        name: "FK_MST_BankAccount_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MST_Supplier",
                columns: table => new
                {
                    SupplierId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Gstin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AddressLine = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Pincode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_MST_Supplier", x => x.SupplierId);
                    table.ForeignKey(
                        name: "FK_MST_Supplier_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Quotation",
                columns: table => new
                {
                    QuotationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    QuotationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CgstPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SgstPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IgstPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    SentUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ViewedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RespondedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RespondedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExpiredUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CustomerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CustomerMobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CustomerGstin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BillingAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyGstin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CompanyAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PlaceOfSupply = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TermsAndConditions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PdfPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TRN_Quotation", x => x.QuotationId);
                    table.ForeignKey(
                        name: "FK_TRN_Quotation_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Quotation_MST_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Quotation_TRN_Invoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "TRN_Invoice",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_PurchaseOrder",
                columns: table => new
                {
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PoDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    ExpectedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    SentUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancelledUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AttachmentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TRN_PurchaseOrder", x => x.PurchaseOrderId);
                    table.ForeignKey(
                        name: "FK_TRN_PurchaseOrder_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_PurchaseOrder_MST_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "MST_Supplier",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_QuotationLine",
                columns: table => new
                {
                    QuotationLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    WorkspaceTypeId = table.Column<int>(type: "int", nullable: true),
                    ServiceItemId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    HsnSacCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DurationValue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    DurationUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_QuotationLine", x => x.QuotationLineId);
                    table.ForeignKey(
                        name: "FK_TRN_QuotationLine_MST_ServiceItem_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "MST_ServiceItem",
                        principalColumn: "ServiceItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_QuotationLine_MST_WorkspaceType_WorkspaceTypeId",
                        column: x => x.WorkspaceTypeId,
                        principalTable: "MST_WorkspaceType",
                        principalColumn: "WorkspaceTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_QuotationLine_TRN_Quotation_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "TRN_Quotation",
                        principalColumn: "QuotationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_QuotationStatusHistory",
                columns: table => new
                {
                    QuotationStatusHistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationId = table.Column<long>(type: "bigint", nullable: false),
                    FromStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    ToStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    ChangedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ChangedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_QuotationStatusHistory", x => x.QuotationStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_TRN_QuotationStatusHistory_TRN_Quotation_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "TRN_Quotation",
                        principalColumn: "QuotationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Expense",
                columns: table => new
                {
                    ExpenseId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExpenseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AttachmentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    EnteredByUserId = table.Column<long>(type: "bigint", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_TRN_Expense", x => x.ExpenseId);
                    table.CheckConstraint("CK_Expense_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_TRN_Expense_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Expense_MST_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "MST_Supplier",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Expense_TRN_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "TRN_PurchaseOrder",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_PurchaseOrderLine",
                columns: table => new
                {
                    PurchaseOrderLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    InventoryItemId = table.Column<int>(type: "int", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    HsnSacCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_PurchaseOrderLine", x => x.PurchaseOrderLineId);
                    table.CheckConstraint("CK_PoLine_Received", "[ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity]");
                    table.ForeignKey(
                        name: "FK_TRN_PurchaseOrderLine_MST_InventoryItem_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "MST_InventoryItem",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_PurchaseOrderLine_TRN_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "TRN_PurchaseOrder",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_BankTransaction",
                columns: table => new
                {
                    BankTransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Direction = table.Column<byte>(type: "tinyint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Narration = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    BankReferenceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReconciliationStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    MatchedPaymentId = table.Column<long>(type: "bigint", nullable: true),
                    MatchedExpenseId = table.Column<long>(type: "bigint", nullable: true),
                    MatchedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    MatchedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReconciledUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ReconciledByUserId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    EnteredByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ImportBatchId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TRN_BankTransaction", x => x.BankTransactionId);
                    table.CheckConstraint("CK_BankTxn_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_BankTxn_SingleMatch", "[MatchedPaymentId] IS NULL OR [MatchedExpenseId] IS NULL");
                    table.ForeignKey(
                        name: "FK_TRN_BankTransaction_MST_BankAccount_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "MST_BankAccount",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_BankTransaction_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_BankTransaction_TRN_Expense_MatchedExpenseId",
                        column: x => x.MatchedExpenseId,
                        principalTable: "TRN_Expense",
                        principalColumn: "ExpenseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_BankTransaction_TRN_Payment_MatchedPaymentId",
                        column: x => x.MatchedPaymentId,
                        principalTable: "TRN_Payment",
                        principalColumn: "PaymentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Invoice_Quotation",
                table: "TRN_Invoice",
                column: "QuotationId",
                unique: true,
                filter: "[QuotationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvTxn_PurchaseOrder",
                table: "TRN_InventoryTransaction",
                column: "PurchaseOrderId",
                filter: "[PurchaseOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MST_BankAccount_LocationId",
                table: "MST_BankAccount",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "UX_BankAccount_Code",
                table: "MST_BankAccount",
                column: "AccountCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Supplier_Location_Name",
                table: "MST_Supplier",
                columns: new[] { "LocationId", "SupplierName" });

            migrationBuilder.CreateIndex(
                name: "UX_Supplier_Code",
                table: "MST_Supplier",
                column: "SupplierCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTxn_Account_Date",
                table: "TRN_BankTransaction",
                columns: new[] { "BankAccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTxn_Reconciliation_Date",
                table: "TRN_BankTransaction",
                columns: new[] { "ReconciliationStatus", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_BankTransaction_LocationId",
                table: "TRN_BankTransaction",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "UX_BankTxn_Account_BankReference",
                table: "TRN_BankTransaction",
                columns: new[] { "BankAccountId", "BankReferenceId" },
                unique: true,
                filter: "[BankReferenceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_BankTxn_MatchedExpense",
                table: "TRN_BankTransaction",
                column: "MatchedExpenseId",
                unique: true,
                filter: "[MatchedExpenseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_BankTxn_MatchedPayment",
                table: "TRN_BankTransaction",
                column: "MatchedPaymentId",
                unique: true,
                filter: "[MatchedPaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Expense_Category_Date",
                table: "TRN_Expense",
                columns: new[] { "Category", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Expense_Location_Date",
                table: "TRN_Expense",
                columns: new[] { "LocationId", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Expense_PurchaseOrderId",
                table: "TRN_Expense",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Expense_SupplierId",
                table: "TRN_Expense",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "UX_Expense_Number",
                table: "TRN_Expense",
                column: "ExpenseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_Location_Status_Date",
                table: "TRN_PurchaseOrder",
                columns: new[] { "LocationId", "Status", "PoDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_Supplier_Date",
                table: "TRN_PurchaseOrder",
                columns: new[] { "SupplierId", "PoDate" });

            migrationBuilder.CreateIndex(
                name: "UX_PurchaseOrder_Number",
                table: "TRN_PurchaseOrder",
                column: "PoNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_PurchaseOrderLine_InventoryItemId",
                table: "TRN_PurchaseOrderLine",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_PurchaseOrderLine_PurchaseOrderId",
                table: "TRN_PurchaseOrderLine",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotation_Customer_Date",
                table: "TRN_Quotation",
                columns: new[] { "CustomerId", "QuotationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotation_Location_Status_Date",
                table: "TRN_Quotation",
                columns: new[] { "LocationId", "Status", "QuotationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotation_ValidUntil_Status",
                table: "TRN_Quotation",
                columns: new[] { "ValidUntil", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Quotation_InvoiceId",
                table: "TRN_Quotation",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "UX_Quotation_Number",
                table: "TRN_Quotation",
                column: "QuotationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_QuotationLine_QuotationId",
                table: "TRN_QuotationLine",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_QuotationLine_ServiceItemId",
                table: "TRN_QuotationLine",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_QuotationLine_WorkspaceTypeId",
                table: "TRN_QuotationLine",
                column: "WorkspaceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationStatusHistory_Quotation",
                table: "TRN_QuotationStatusHistory",
                column: "QuotationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TRN_BankTransaction");

            migrationBuilder.DropTable(
                name: "TRN_PurchaseOrderLine");

            migrationBuilder.DropTable(
                name: "TRN_QuotationLine");

            migrationBuilder.DropTable(
                name: "TRN_QuotationStatusHistory");

            migrationBuilder.DropTable(
                name: "MST_BankAccount");

            migrationBuilder.DropTable(
                name: "TRN_Expense");

            migrationBuilder.DropTable(
                name: "TRN_Quotation");

            migrationBuilder.DropTable(
                name: "TRN_PurchaseOrder");

            migrationBuilder.DropTable(
                name: "MST_Supplier");

            migrationBuilder.DropIndex(
                name: "UX_Invoice_Quotation",
                table: "TRN_Invoice");

            migrationBuilder.DropIndex(
                name: "IX_InvTxn_PurchaseOrder",
                table: "TRN_InventoryTransaction");

            migrationBuilder.DropColumn(
                name: "QuotationId",
                table: "TRN_Invoice");

            migrationBuilder.DropColumn(
                name: "TermsAndConditions",
                table: "TRN_Invoice");

            migrationBuilder.DropColumn(
                name: "AttachmentPath",
                table: "TRN_InventoryTransaction");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceNumber",
                table: "TRN_InventoryTransaction");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                table: "TRN_InventoryTransaction");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "TRN_InventoryTransaction");
        }
    }
}
