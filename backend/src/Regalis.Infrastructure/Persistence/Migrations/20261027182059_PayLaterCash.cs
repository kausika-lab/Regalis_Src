using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PayLaterCash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "TRN_Booking",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsPayLater",
                table: "TRN_Booking",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayLaterReminderSentUtc",
                table: "TRN_Booking",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDueUtc",
                table: "TRN_Booking",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PayLaterAllowed",
                table: "MST_User",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "TRN_CashRegisterSession",
                columns: table => new
                {
                    CashRegisterSessionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OpeningFloat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OpenedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ExpectedCash = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CountedCash = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Variance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true, computedColumnSql: "[CountedCash] - [ExpectedCash]", stored: true),
                    VarianceReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ClosedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    HandedOverToUserId = table.Column<long>(type: "bigint", nullable: true),
                    HandedOverUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_TRN_CashRegisterSession", x => x.CashRegisterSessionId);
                    table.CheckConstraint("CK_CashSession_Closed", "[Status] = 1 OR ([ClosedUtc] IS NOT NULL AND [ExpectedCash] IS NOT NULL AND [CountedCash] IS NOT NULL)");
                    table.CheckConstraint("CK_CashSession_Float", "[OpeningFloat] >= 0");
                    table.CheckConstraint("CK_CashSession_VarianceReason", "[Status] = 1 OR [CountedCash] = [ExpectedCash] OR [VarianceReason] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_TRN_CashRegisterSession_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_CashRegisterSession_MST_User_HandedOverToUserId",
                        column: x => x.HandedOverToUserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_CashRegisterSession_MST_User_UserId",
                        column: x => x.UserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_CashCollection",
                columns: table => new
                {
                    CashCollectionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    PaymentMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountTendered = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ChangeReturned = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CollectedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CollectedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CashRegisterSessionId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    VoidedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    VoidedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TRN_CashCollection", x => x.CashCollectionId);
                    table.CheckConstraint("CK_CashCollection_Amount", "[Amount] <> 0");
                    table.CheckConstraint("CK_CashCollection_Change", "[AmountTendered] IS NULL OR ([PaymentMethod] = 6 AND [AmountTendered] - ISNULL([ChangeReturned], 0) = [Amount])");
                    table.CheckConstraint("CK_CashCollection_Method", "[PaymentMethod] IN (2,3,4,6)");
                    table.CheckConstraint("CK_CashCollection_Reference", "[PaymentMethod] = 6 OR [ReferenceNumber] IS NOT NULL");
                    table.CheckConstraint("CK_CashCollection_Void", "([Status] = 2 AND [VoidedByUserId] IS NOT NULL AND [VoidedUtc] IS NOT NULL AND [VoidReason] IS NOT NULL) OR ([Status] = 1 AND [VoidedUtc] IS NULL)");
                    table.ForeignKey(
                        name: "FK_TRN_CashCollection_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_CashCollection_MST_User_CollectedByUserId",
                        column: x => x.CollectedByUserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_CashCollection_MST_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_CashCollection_TRN_Booking_BookingId",
                        column: x => x.BookingId,
                        principalTable: "TRN_Booking",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_CashCollection_TRN_CashRegisterSession_CashRegisterSessionId",
                        column: x => x.CashRegisterSessionId,
                        principalTable: "TRN_CashRegisterSession",
                        principalColumn: "CashRegisterSessionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_CashCollection_TRN_Invoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "TRN_Invoice",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_CashCollection_TRN_Payment_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "TRN_Payment",
                        principalColumn: "PaymentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Booking_PayLater_Due",
                table: "TRN_Booking",
                column: "PaymentDueUtc",
                filter: "[IsPayLater] = 1 AND [IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "CustomerId", "Status", "TotalAmount", "AmountPaid" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Booking_AmountPaid",
                table: "TRN_Booking",
                sql: "[AmountPaid] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_CashCollection_Booking",
                table: "TRN_CashCollection",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_CashCollection_Collector_Date",
                table: "TRN_CashCollection",
                columns: new[] { "CollectedByUserId", "CollectedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashCollection_Location_Date",
                table: "TRN_CashCollection",
                columns: new[] { "LocationId", "CollectedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashCollection_Session",
                table: "TRN_CashCollection",
                column: "CashRegisterSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_CashCollection_CustomerId",
                table: "TRN_CashCollection",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_CashCollection_InvoiceId",
                table: "TRN_CashCollection",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_CashCollection_PaymentId",
                table: "TRN_CashCollection",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "UX_CashCollection_Receipt",
                table: "TRN_CashCollection",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashSession_Status_Date",
                table: "TRN_CashRegisterSession",
                columns: new[] { "Status", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_CashRegisterSession_HandedOverToUserId",
                table: "TRN_CashRegisterSession",
                column: "HandedOverToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_CashRegisterSession_UserId",
                table: "TRN_CashRegisterSession",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_CashSession_Location_User_Date",
                table: "TRN_CashRegisterSession",
                columns: new[] { "LocationId", "UserId", "BusinessDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CashSession_Number",
                table: "TRN_CashRegisterSession",
                column: "SessionNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TRN_CashCollection");

            migrationBuilder.DropTable(
                name: "TRN_CashRegisterSession");

            migrationBuilder.DropIndex(
                name: "IX_Booking_PayLater_Due",
                table: "TRN_Booking");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Booking_AmountPaid",
                table: "TRN_Booking");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "TRN_Booking");

            migrationBuilder.DropColumn(
                name: "IsPayLater",
                table: "TRN_Booking");

            migrationBuilder.DropColumn(
                name: "PayLaterReminderSentUtc",
                table: "TRN_Booking");

            migrationBuilder.DropColumn(
                name: "PaymentDueUtc",
                table: "TRN_Booking");

            migrationBuilder.DropColumn(
                name: "PayLaterAllowed",
                table: "MST_User");
        }
    }
}
