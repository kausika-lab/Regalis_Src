using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SignedAdjustmentQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InvTxn_Quantity",
                table: "TRN_InventoryTransaction");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvTxn_Quantity",
                table: "TRN_InventoryTransaction",
                sql: "[Quantity] > 0 OR ([TransactionType] = 5 AND [Quantity] <> 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InvTxn_Quantity",
                table: "TRN_InventoryTransaction");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvTxn_Quantity",
                table: "TRN_InventoryTransaction",
                sql: "[Quantity] > 0");
        }
    }
}
