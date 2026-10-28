using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PayLaterPasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "TRN_CustomerPass",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsPayLater",
                table: "TRN_CustomerPass",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDueUtc",
                table: "TRN_CustomerPass",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "BookingId",
                table: "TRN_CashCollection",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "CustomerPassId",
                table: "TRN_CashCollection",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPass_PayLater_Due",
                table: "TRN_CustomerPass",
                column: "PaymentDueUtc",
                filter: "[IsPayLater] = 1 AND [IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "CustomerId", "Status", "TotalAmount", "AmountPaid" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerPass_AmountPaid",
                table: "TRN_CustomerPass",
                sql: "[AmountPaid] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_CashCollection_CustomerPass",
                table: "TRN_CashCollection",
                column: "CustomerPassId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashCollection_Target",
                table: "TRN_CashCollection",
                sql: "([BookingId] IS NOT NULL AND [CustomerPassId] IS NULL) OR ([BookingId] IS NULL AND [CustomerPassId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_TRN_CashCollection_TRN_CustomerPass_CustomerPassId",
                table: "TRN_CashCollection",
                column: "CustomerPassId",
                principalTable: "TRN_CustomerPass",
                principalColumn: "CustomerPassId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TRN_CashCollection_TRN_CustomerPass_CustomerPassId",
                table: "TRN_CashCollection");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPass_PayLater_Due",
                table: "TRN_CustomerPass");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerPass_AmountPaid",
                table: "TRN_CustomerPass");

            migrationBuilder.DropIndex(
                name: "IX_CashCollection_CustomerPass",
                table: "TRN_CashCollection");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashCollection_Target",
                table: "TRN_CashCollection");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "TRN_CustomerPass");

            migrationBuilder.DropColumn(
                name: "IsPayLater",
                table: "TRN_CustomerPass");

            migrationBuilder.DropColumn(
                name: "PaymentDueUtc",
                table: "TRN_CustomerPass");

            migrationBuilder.DropColumn(
                name: "CustomerPassId",
                table: "TRN_CashCollection");

            migrationBuilder.AlterColumn<long>(
                name: "BookingId",
                table: "TRN_CashCollection",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
