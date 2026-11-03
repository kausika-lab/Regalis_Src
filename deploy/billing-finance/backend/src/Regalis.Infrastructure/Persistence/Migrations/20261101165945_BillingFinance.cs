using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillingFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MST_BankColumnMapping",
                columns: table => new
                {
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    MappingJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DateFormat = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_MST_BankColumnMapping", x => x.BankAccountId);
                    table.ForeignKey(
                        name: "FK_MST_BankColumnMapping_MST_BankAccount_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "MST_BankAccount",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MST_ComplianceObligation",
                columns: table => new
                {
                    ComplianceObligationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DueDay = table.Column<int>(type: "int", nullable: false),
                    DueMonths = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    AmountSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OwnerRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_MST_ComplianceObligation", x => x.ComplianceObligationId);
                });

            migrationBuilder.CreateTable(
                name: "MST_CustomerTaxProfile",
                columns: table => new
                {
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Tan = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DoNotChase = table.Column<bool>(type: "bit", nullable: false),
                    DoNotChaseReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_MST_CustomerTaxProfile", x => x.CustomerId);
                    table.CheckConstraint("CK_CustomerTaxProfile_Tan", "[Tan] IS NULL OR [Tan] LIKE '[A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9][0-9][A-Z]'");
                    table.ForeignKey(
                        name: "FK_MST_CustomerTaxProfile_MST_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MST_Department",
                columns: table => new
                {
                    DepartmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
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
                    table.PrimaryKey("PK_MST_Department", x => x.DepartmentId);
                });

            migrationBuilder.CreateTable(
                name: "MST_DifferenceReason",
                columns: table => new
                {
                    DifferenceReasonId = table.Column<byte>(type: "tinyint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Help = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RequiredPermission = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_DifferenceReason", x => x.DifferenceReasonId);
                });

            migrationBuilder.CreateTable(
                name: "MST_GstRate",
                columns: table => new
                {
                    GstRateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Rate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_GstRate", x => x.GstRateId);
                });

            migrationBuilder.CreateTable(
                name: "MST_SupplierTaxProfile",
                columns: table => new
                {
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    Pan = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DefaultTdsSection = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PaymentTermsDays = table.Column<int>(type: "int", nullable: true),
                    StateCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsIndividual = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_MST_SupplierTaxProfile", x => x.SupplierId);
                    table.CheckConstraint("CK_SupplierTaxProfile_Pan", "[Pan] IS NULL OR [Pan] LIKE '[A-Z][A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9][A-Z]'");
                    table.ForeignKey(
                        name: "FK_MST_SupplierTaxProfile_MST_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "MST_Supplier",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MST_TdsSection",
                columns: table => new
                {
                    TdsSectionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Section = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IndividualRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    NoPanRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_TdsSection", x => x.TdsSectionId);
                });

            migrationBuilder.CreateTable(
                name: "TRN_BankImportBatch",
                columns: table => new
                {
                    BankImportBatchId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    ImportedCount = table.Column<int>(type: "int", nullable: false),
                    SkippedCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    FirstDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FileClosingBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ColumnMappingJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ImportedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ImportedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    IsUndone = table.Column<bool>(type: "bit", nullable: false),
                    UndoneByUserId = table.Column<long>(type: "bigint", nullable: true),
                    UndoneUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
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
                    table.PrimaryKey("PK_TRN_BankImportBatch", x => x.BankImportBatchId);
                    table.ForeignKey(
                        name: "FK_TRN_BankImportBatch_MST_BankAccount_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "MST_BankAccount",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_BankMatchAllocation",
                columns: table => new
                {
                    BankMatchAllocationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankTransactionId = table.Column<long>(type: "bigint", nullable: false),
                    DocType = table.Column<byte>(type: "tinyint", nullable: false),
                    DocId = table.Column<long>(type: "bigint", nullable: false),
                    DocNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedRefs = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UnmatchReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_TRN_BankMatchAllocation", x => x.BankMatchAllocationId);
                    table.CheckConstraint("CK_BankMatchAllocation_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_TRN_BankMatchAllocation_TRN_BankTransaction_BankTransactionId",
                        column: x => x.BankTransactionId,
                        principalTable: "TRN_BankTransaction",
                        principalColumn: "BankTransactionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_BankMatchDifference",
                columns: table => new
                {
                    BankMatchDifferenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankTransactionId = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<byte>(type: "tinyint", nullable: false),
                    SignedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TdsSection = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TargetDocType = table.Column<byte>(type: "tinyint", nullable: true),
                    TargetDocId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedExpenseId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedTdsEntryId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedWalletTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedVendorPaymentId = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_TRN_BankMatchDifference", x => x.BankMatchDifferenceId);
                    table.CheckConstraint("CK_BankMatchDifference_Amount", "[SignedAmount] <> 0");
                    table.ForeignKey(
                        name: "FK_TRN_BankMatchDifference_TRN_BankTransaction_BankTransactionId",
                        column: x => x.BankTransactionId,
                        principalTable: "TRN_BankTransaction",
                        principalColumn: "BankTransactionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_ExpenseReceipt",
                columns: table => new
                {
                    ExpenseReceiptId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    UploadedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_ExpenseReceipt", x => x.ExpenseReceiptId);
                    table.ForeignKey(
                        name: "FK_TRN_ExpenseReceipt_TRN_Expense_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "TRN_Expense",
                        principalColumn: "ExpenseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Gstr2bLine",
                columns: table => new
                {
                    Gstr2bLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Period = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    SupplierGstin = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BillNo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    BillDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TaxableValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Cgst = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Sgst = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Igst = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MatchedBillId = table.Column<long>(type: "bigint", nullable: true),
                    Result = table.Column<byte>(type: "tinyint", nullable: false),
                    UploadedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_Gstr2bLine", x => x.Gstr2bLineId);
                });

            migrationBuilder.CreateTable(
                name: "TRN_GstReturn",
                columns: table => new
                {
                    GstReturnId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Period = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    ReturnType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Arn = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FiledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    FiledByUserId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_TRN_GstReturn", x => x.GstReturnId);
                    table.CheckConstraint("CK_GstReturn_Filed", "[Status] <> 3 OR ([Arn] IS NOT NULL AND [FiledOn] IS NOT NULL)");
                    table.CheckConstraint("CK_GstReturn_Type", "[ReturnType] IN ('GSTR1','GSTR3B')");
                });

            migrationBuilder.CreateTable(
                name: "TRN_InvoicePayment",
                columns: table => new
                {
                    InvoicePaymentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Mode = table.Column<byte>(type: "tinyint", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    BankAccountId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    WalletTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    CashCollectionId = table.Column<long>(type: "bigint", nullable: true),
                    BankTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    ReversedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReversedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ReverseReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ReversalOfId = table.Column<long>(type: "bigint", nullable: true),
                    RecordedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    RecordedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
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
                    table.PrimaryKey("PK_TRN_InvoicePayment", x => x.InvoicePaymentId);
                    table.CheckConstraint("CK_InvoicePayment_Amount", "[Amount] <> 0");
                    table.CheckConstraint("CK_InvoicePayment_Mode", "[Mode] BETWEEN 1 AND 8");
                    table.CheckConstraint("CK_InvoicePayment_Reference", "[Mode] IN (1,6,7,8) OR [ReversalOfId] IS NOT NULL OR [Reference] IS NOT NULL");
                    table.CheckConstraint("CK_InvoicePayment_Reversal", "([ReversalOfId] IS NULL AND [Amount] > 0) OR ([ReversalOfId] IS NOT NULL AND [Amount] < 0)");
                    table.ForeignKey(
                        name: "FK_TRN_InvoicePayment_MST_BankAccount_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "MST_BankAccount",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_InvoicePayment_MST_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_InvoicePayment_TRN_InvoicePayment_ReversalOfId",
                        column: x => x.ReversalOfId,
                        principalTable: "TRN_InvoicePayment",
                        principalColumn: "InvoicePaymentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_InvoicePayment_TRN_Invoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "TRN_Invoice",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_InvoicePayment_TRN_Payment_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "TRN_Payment",
                        principalColumn: "PaymentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_PeriodLock",
                columns: table => new
                {
                    PeriodLockId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Period = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    LockedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UnlockedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    UnlockedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UnlockReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_TRN_PeriodLock", x => x.PeriodLockId);
                    table.CheckConstraint("CK_PeriodLock_Period", "[Period] LIKE '[12][0-9][0-9][0-9]-[01][0-9]'");
                });

            migrationBuilder.CreateTable(
                name: "TRN_PettyCashEntry",
                columns: table => new
                {
                    PettyCashEntryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntryType = table.Column<byte>(type: "tinyint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HappenedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ExpenseId = table.Column<long>(type: "bigint", nullable: true),
                    VendorPaymentId = table.Column<long>(type: "bigint", nullable: true),
                    BankAccountId = table.Column<int>(type: "int", nullable: true),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CountedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EnteredByUserId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_TRN_PettyCashEntry", x => x.PettyCashEntryId);
                    table.CheckConstraint("CK_PettyCash_NotNegative", "[BalanceAfter] >= 0");
                    table.CheckConstraint("CK_PettyCash_Sign", "([EntryType] = 1 AND [Amount] > 0) OR ([EntryType] = 2 AND [Amount] < 0) OR [EntryType] = 3");
                });

            migrationBuilder.CreateTable(
                name: "TRN_PurchaseBill",
                columns: table => new
                {
                    PurchaseBillId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    SupplierBillNo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SupplierBillNoKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    BillDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentTermsDays = table.Column<int>(type: "int", nullable: false),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    SupplierStateCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    PlaceOfSupplyCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsInterState = table.Column<bool>(type: "bit", nullable: false),
                    SupplierGstin = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    TaxableValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BillTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TdsSection = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TdsRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TdsAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    EnteredByUserId = table.Column<long>(type: "bigint", nullable: false),
                    SubmittedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ApprovedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ApprovalComment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HoldReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TRN_PurchaseBill", x => x.PurchaseBillId);
                    table.CheckConstraint("CK_PurchaseBill_Dates", "[DueDate] >= [BillDate]");
                    table.CheckConstraint("CK_PurchaseBill_Status", "[Status] BETWEEN 1 AND 8");
                    table.CheckConstraint("CK_PurchaseBill_Total", "[BillTotal] >= 0 AND [TaxableValue] >= 0 AND [TdsAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_TRN_PurchaseBill_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_PurchaseBill_MST_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "MST_Supplier",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_PurchaseBill_TRN_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "TRN_PurchaseOrder",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_RecurringExpense",
                columns: table => new
                {
                    RecurringExpenseId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    DayOfMonth = table.Column<int>(type: "int", nullable: false),
                    StartOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndOn = table.Column<DateOnly>(type: "date", nullable: true),
                    LastRaisedFor = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_TRN_RecurringExpense", x => x.RecurringExpenseId);
                    table.CheckConstraint("CK_RecurringExpense_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_RecurringExpense_Day", "[DayOfMonth] BETWEEN 1 AND 28");
                });

            migrationBuilder.CreateTable(
                name: "TRN_ReminderLog",
                columns: table => new
                {
                    ReminderLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReminderType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Recipient = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SentUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Delivered = table.Column<bool>(type: "bit", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_ReminderLog", x => x.ReminderLogId);
                });

            migrationBuilder.CreateTable(
                name: "TRN_TdsChallan",
                columns: table => new
                {
                    TdsChallanId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BsrCode = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    ChallanNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quarter = table.Column<byte>(type: "tinyint", nullable: false),
                    FinancialYear = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
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
                    table.PrimaryKey("PK_TRN_TdsChallan", x => x.TdsChallanId);
                    table.CheckConstraint("CK_TdsChallan_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_TdsChallan_Bsr", "LEN([BsrCode]) = 7");
                });

            migrationBuilder.CreateTable(
                name: "TRN_VendorPayment",
                columns: table => new
                {
                    VendorPaymentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TdsAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceAdjusted = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Mode = table.Column<byte>(type: "tinyint", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    BankAccountId = table.Column<int>(type: "int", nullable: true),
                    FromPettyCash = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    ReversedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReversedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ReverseReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PaidByUserId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_TRN_VendorPayment", x => x.VendorPaymentId);
                    table.CheckConstraint("CK_VendorPayment_Amounts", "[Amount] >= 0 AND [TdsAmount] >= 0 AND [AdvanceAdjusted] >= 0 AND [AdvanceAmount] >= 0 AND [AdvanceAmount] <= [Amount]");
                    table.CheckConstraint("CK_VendorPayment_Reference", "[Mode] = 6 OR [Reference] IS NOT NULL");
                    table.CheckConstraint("CK_VendorPayment_Source", "([FromPettyCash] = 1 AND [BankAccountId] IS NULL) OR ([FromPettyCash] = 0 AND [BankAccountId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TRN_VendorPayment_MST_BankAccount_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "MST_BankAccount",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_VendorPayment_MST_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "MST_Supplier",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_ComplianceStatus",
                columns: table => new
                {
                    ComplianceStatusId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComplianceObligationId = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CompletedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
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
                    table.PrimaryKey("PK_TRN_ComplianceStatus", x => x.ComplianceStatusId);
                    table.ForeignKey(
                        name: "FK_TRN_ComplianceStatus_MST_ComplianceObligation_ComplianceObligationId",
                        column: x => x.ComplianceObligationId,
                        principalTable: "MST_ComplianceObligation",
                        principalColumn: "ComplianceObligationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_ExpenseControl",
                columns: table => new
                {
                    ExpenseId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    ApprovalStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    ApprovalLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    DecidedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    DecidedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DecisionComment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaidFrom = table.Column<byte>(type: "tinyint", nullable: false),
                    IsReimbursement = table.Column<bool>(type: "bit", nullable: false),
                    ReimburseToUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReimbursedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ReimbursedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReimbursedVia = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReimbursementReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    RecurringExpenseId = table.Column<long>(type: "bigint", nullable: true),
                    IsRecurringDraft = table.Column<bool>(type: "bit", nullable: false),
                    PettyCashEntryId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_TRN_ExpenseControl", x => x.ExpenseId);
                    table.CheckConstraint("CK_ExpenseControl_Decision", "[ApprovalStatus] <> 3 OR [DecisionComment] IS NOT NULL");
                    table.CheckConstraint("CK_ExpenseControl_Reimburse", "[IsReimbursement] = 0 OR [ReimburseToUserId] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_TRN_ExpenseControl_MST_Department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "MST_Department",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_ExpenseControl_TRN_Expense_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "TRN_Expense",
                        principalColumn: "ExpenseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_PurchaseBillAttachment",
                columns: table => new
                {
                    PurchaseBillAttachmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseBillId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    UploadedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_PurchaseBillAttachment", x => x.PurchaseBillAttachmentId);
                    table.ForeignKey(
                        name: "FK_TRN_PurchaseBillAttachment_TRN_PurchaseBill_PurchaseBillId",
                        column: x => x.PurchaseBillId,
                        principalTable: "TRN_PurchaseBill",
                        principalColumn: "PurchaseBillId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_PurchaseBillEvent",
                columns: table => new
                {
                    PurchaseBillEventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseBillId = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    EventUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_PurchaseBillEvent", x => x.PurchaseBillEventId);
                    table.ForeignKey(
                        name: "FK_TRN_PurchaseBillEvent_TRN_PurchaseBill_PurchaseBillId",
                        column: x => x.PurchaseBillId,
                        principalTable: "TRN_PurchaseBill",
                        principalColumn: "PurchaseBillId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_PurchaseBillLine",
                columns: table => new
                {
                    PurchaseBillLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseBillId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    HsnSac = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxableValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GstRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_PurchaseBillLine", x => x.PurchaseBillLineId);
                    table.CheckConstraint("CK_PurchaseBillLine_Qty", "[Quantity] > 0 AND [Rate] >= 0");
                    table.ForeignKey(
                        name: "FK_TRN_PurchaseBillLine_TRN_PurchaseBill_PurchaseBillId",
                        column: x => x.PurchaseBillId,
                        principalTable: "TRN_PurchaseBill",
                        principalColumn: "PurchaseBillId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_TdsEntry",
                columns: table => new
                {
                    TdsEntryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Direction = table.Column<byte>(type: "tinyint", nullable: false),
                    PartyType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PartyId = table.Column<long>(type: "bigint", nullable: false),
                    PartyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartyTaxId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Section = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TdsAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Quarter = table.Column<byte>(type: "tinyint", nullable: false),
                    FinancialYear = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    TdsChallanId = table.Column<long>(type: "bigint", nullable: true),
                    CertificateNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CertificateReceivedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CertificatePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CertificateFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
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
                    table.PrimaryKey("PK_TRN_TdsEntry", x => x.TdsEntryId);
                    table.CheckConstraint("CK_TdsEntry_Amount", "[TdsAmount] > 0 AND [BaseAmount] >= 0");
                    table.CheckConstraint("CK_TdsEntry_Quarter", "[Quarter] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_TRN_TdsEntry_TRN_TdsChallan_TdsChallanId",
                        column: x => x.TdsChallanId,
                        principalTable: "TRN_TdsChallan",
                        principalColumn: "TdsChallanId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_VendorPaymentAllocation",
                columns: table => new
                {
                    VendorPaymentAllocationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VendorPaymentId = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseBillId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TdsAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_VendorPaymentAllocation", x => x.VendorPaymentAllocationId);
                    table.CheckConstraint("CK_VendorPaymentAllocation_Amount", "[Amount] > 0 AND [TdsAmount] >= 0 AND [TdsAmount] <= [Amount]");
                    table.ForeignKey(
                        name: "FK_TRN_VendorPaymentAllocation_TRN_PurchaseBill_PurchaseBillId",
                        column: x => x.PurchaseBillId,
                        principalTable: "TRN_PurchaseBill",
                        principalColumn: "PurchaseBillId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_VendorPaymentAllocation_TRN_VendorPayment_VendorPaymentId",
                        column: x => x.VendorPaymentId,
                        principalTable: "TRN_VendorPayment",
                        principalColumn: "VendorPaymentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ComplianceObligation_Code",
                table: "MST_ComplianceObligation",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Department_Name",
                table: "MST_Department",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_DifferenceReason_Code",
                table: "MST_DifferenceReason",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_GstRate_Rate_From",
                table: "MST_GstRate",
                columns: new[] { "Rate", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_TdsSection_Section_From",
                table: "MST_TdsSection",
                columns: new[] { "Section", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_BankImportBatch_BankAccountId",
                table: "TRN_BankImportBatch",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_BankImportBatch_Code",
                table: "TRN_BankImportBatch",
                column: "BatchCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankMatchAllocation_Doc",
                table: "TRN_BankMatchAllocation",
                columns: new[] { "DocType", "DocId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankMatchAllocation_Line",
                table: "TRN_BankMatchAllocation",
                column: "BankTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BankMatchDifference_Line",
                table: "TRN_BankMatchDifference",
                column: "BankTransactionId");

            migrationBuilder.CreateIndex(
                name: "UX_ComplianceStatus_Obligation_Period",
                table: "TRN_ComplianceStatus",
                columns: new[] { "ComplianceObligationId", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseControl_Status",
                table: "TRN_ExpenseControl",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_ExpenseControl_DepartmentId",
                table: "TRN_ExpenseControl",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseReceipt_Expense",
                table: "TRN_ExpenseReceipt",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_Gstr2bLine_Period",
                table: "TRN_Gstr2bLine",
                column: "Period");

            migrationBuilder.CreateIndex(
                name: "UX_GstReturn_Period_Type",
                table: "TRN_GstReturn",
                columns: new[] { "Period", "ReturnType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayment_Customer_Date",
                table: "TRN_InvoicePayment",
                columns: new[] { "CustomerId", "PaidOn" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayment_Invoice",
                table: "TRN_InvoicePayment",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayment_Payment",
                table: "TRN_InvoicePayment",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_InvoicePayment_BankAccountId",
                table: "TRN_InvoicePayment",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_InvoicePayment_ReversalOfId",
                table: "TRN_InvoicePayment",
                column: "ReversalOfId");

            migrationBuilder.CreateIndex(
                name: "UX_InvoicePayment_Receipt",
                table: "TRN_InvoicePayment",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PeriodLock_Period",
                table: "TRN_PeriodLock",
                column: "Period",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PettyCash_Date",
                table: "TRN_PettyCashEntry",
                column: "HappenedOn");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseBill_BillDate",
                table: "TRN_PurchaseBill",
                column: "BillDate");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseBill_Status_Due",
                table: "TRN_PurchaseBill",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_PurchaseBill_LocationId",
                table: "TRN_PurchaseBill",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_PurchaseBill_PurchaseOrderId",
                table: "TRN_PurchaseBill",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "UX_PurchaseBill_Number",
                table: "TRN_PurchaseBill",
                column: "BillNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PurchaseBill_Supplier_BillNo",
                table: "TRN_PurchaseBill",
                columns: new[] { "SupplierId", "SupplierBillNoKey" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] <> 8");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_PurchaseBillAttachment_PurchaseBillId",
                table: "TRN_PurchaseBillAttachment",
                column: "PurchaseBillId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_PurchaseBillEvent_PurchaseBillId",
                table: "TRN_PurchaseBillEvent",
                column: "PurchaseBillId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_PurchaseBillLine_PurchaseBillId",
                table: "TRN_PurchaseBillLine",
                column: "PurchaseBillId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderLog_Document",
                table: "TRN_ReminderLog",
                columns: new[] { "DocumentType", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "UX_TdsChallan_Bsr_No_Date",
                table: "TRN_TdsChallan",
                columns: new[] { "BsrCode", "ChallanNo", "PaidOn" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TdsEntry_Challan",
                table: "TRN_TdsEntry",
                column: "TdsChallanId");

            migrationBuilder.CreateIndex(
                name: "IX_TdsEntry_Direction_Period",
                table: "TRN_TdsEntry",
                columns: new[] { "Direction", "FinancialYear", "Quarter" });

            migrationBuilder.CreateIndex(
                name: "IX_TdsEntry_Source",
                table: "TRN_TdsEntry",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_VendorPayment_BankAccountId",
                table: "TRN_VendorPayment",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPayment_Supplier_Date",
                table: "TRN_VendorPayment",
                columns: new[] { "SupplierId", "PaidOn" });

            migrationBuilder.CreateIndex(
                name: "UX_VendorPayment_Number",
                table: "TRN_VendorPayment",
                column: "PaymentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorPaymentAllocation_Bill",
                table: "TRN_VendorPaymentAllocation",
                column: "PurchaseBillId");

            migrationBuilder.CreateIndex(
                name: "UX_VendorPaymentAllocation_Payment_Bill",
                table: "TRN_VendorPaymentAllocation",
                columns: new[] { "VendorPaymentId", "PurchaseBillId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MST_BankColumnMapping");

            migrationBuilder.DropTable(
                name: "MST_CustomerTaxProfile");

            migrationBuilder.DropTable(
                name: "MST_DifferenceReason");

            migrationBuilder.DropTable(
                name: "MST_GstRate");

            migrationBuilder.DropTable(
                name: "MST_SupplierTaxProfile");

            migrationBuilder.DropTable(
                name: "MST_TdsSection");

            migrationBuilder.DropTable(
                name: "TRN_BankImportBatch");

            migrationBuilder.DropTable(
                name: "TRN_BankMatchAllocation");

            migrationBuilder.DropTable(
                name: "TRN_BankMatchDifference");

            migrationBuilder.DropTable(
                name: "TRN_ComplianceStatus");

            migrationBuilder.DropTable(
                name: "TRN_ExpenseControl");

            migrationBuilder.DropTable(
                name: "TRN_ExpenseReceipt");

            migrationBuilder.DropTable(
                name: "TRN_Gstr2bLine");

            migrationBuilder.DropTable(
                name: "TRN_GstReturn");

            migrationBuilder.DropTable(
                name: "TRN_InvoicePayment");

            migrationBuilder.DropTable(
                name: "TRN_PeriodLock");

            migrationBuilder.DropTable(
                name: "TRN_PettyCashEntry");

            migrationBuilder.DropTable(
                name: "TRN_PurchaseBillAttachment");

            migrationBuilder.DropTable(
                name: "TRN_PurchaseBillEvent");

            migrationBuilder.DropTable(
                name: "TRN_PurchaseBillLine");

            migrationBuilder.DropTable(
                name: "TRN_RecurringExpense");

            migrationBuilder.DropTable(
                name: "TRN_ReminderLog");

            migrationBuilder.DropTable(
                name: "TRN_TdsEntry");

            migrationBuilder.DropTable(
                name: "TRN_VendorPaymentAllocation");

            migrationBuilder.DropTable(
                name: "MST_ComplianceObligation");

            migrationBuilder.DropTable(
                name: "MST_Department");

            migrationBuilder.DropTable(
                name: "TRN_TdsChallan");

            migrationBuilder.DropTable(
                name: "TRN_PurchaseBill");

            migrationBuilder.DropTable(
                name: "TRN_VendorPayment");
        }
    }
}
