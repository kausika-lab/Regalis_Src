using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpenseAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdjustmentTotal",
                table: "TRN_Expense",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "TRN_ExpenseAdjustment",
                columns: table => new
                {
                    ExpenseAdjustmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseId = table.Column<long>(type: "bigint", nullable: false),
                    AdjustmentType = table.Column<byte>(type: "tinyint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdjustmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_TRN_ExpenseAdjustment", x => x.ExpenseAdjustmentId);
                    table.CheckConstraint("CK_ExpenseAdjustment_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_ExpenseAdjustment_Type", "[AdjustmentType] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_TRN_ExpenseAdjustment_TRN_Expense_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "TRN_Expense",
                        principalColumn: "ExpenseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Expense_NetAmount",
                table: "TRN_Expense",
                sql: "[Amount] + [AdjustmentTotal] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseAdjustment_Date",
                table: "TRN_ExpenseAdjustment",
                column: "AdjustmentDate");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseAdjustment_Expense_Date",
                table: "TRN_ExpenseAdjustment",
                columns: new[] { "ExpenseId", "AdjustmentDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TRN_ExpenseAdjustment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Expense_NetAmount",
                table: "TRN_Expense");

            migrationBuilder.DropColumn(
                name: "AdjustmentTotal",
                table: "TRN_Expense");
        }
    }
}
