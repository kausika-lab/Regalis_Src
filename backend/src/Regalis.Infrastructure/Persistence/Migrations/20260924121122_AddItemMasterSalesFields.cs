using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemMasterSalesFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InventoryItemId",
                table: "TRN_QuotationLine",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNonTaxable",
                table: "TRN_QuotationLine",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte>(
                name: "ItemType",
                table: "TRN_QuotationLine",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "TRN_QuotationLine",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HsnSacCode",
                table: "MST_InventoryItem",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNonTaxable",
                table: "MST_InventoryItem",
                type: "bit",
                nullable: false,
                // Existing items have no tax set up yet: Non-Taxable until someone edits them.
                defaultValue: true);

            migrationBuilder.AddColumn<byte>(
                name: "ItemType",
                table: "MST_InventoryItem",
                type: "tinyint",
                nullable: false,
                // 0 is not an InventoryItemType; every existing item is stock, i.e. Goods.
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<decimal>(
                name: "SellingRate",
                table: "MST_InventoryItem",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxPercent",
                table: "MST_InventoryItem",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationLine_InventoryItem",
                table: "TRN_QuotationLine",
                column: "InventoryItemId",
                filter: "[InventoryItemId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuotationLine_InventoryItem",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "InventoryItemId",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "IsNonTaxable",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "TRN_QuotationLine");

            migrationBuilder.DropColumn(
                name: "HsnSacCode",
                table: "MST_InventoryItem");

            migrationBuilder.DropColumn(
                name: "IsNonTaxable",
                table: "MST_InventoryItem");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "MST_InventoryItem");

            migrationBuilder.DropColumn(
                name: "SellingRate",
                table: "MST_InventoryItem");

            migrationBuilder.DropColumn(
                name: "TaxPercent",
                table: "MST_InventoryItem");
        }
    }
}
