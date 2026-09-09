using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the structured address, date of birth, gender and GSTIN columns to
    /// MST_User. Every column is nullable and additive: existing rows keep their
    /// data, the existing single-string Address column is left untouched, and no
    /// existing column is renamed, retyped or dropped.
    ///
    /// Column types follow the conventions already in the schema:
    ///   AddressLine1/2  -> MST_Location.AddressLine1/2  nvarchar(200)
    ///   City / State    -> MST_Location.City / State     nvarchar(100)
    ///   PostalCode      -> MST_Location.PostalCode       nvarchar(20)
    ///   Gstin           -> MST_Supplier.Gstin, TRN_Invoice.CustomerGstin nvarchar(20)
    ///   DateOfBirth     -> TRN_CustomerPass.ValidFrom    date
    ///   Gender          -> MST_User.UserType / Status    tinyint (byte enum)
    ///
    /// Country has no precedent in the schema (MST_Location has none), so it
    /// follows City/State at nvarchar(100).
    ///
    /// Drop this file into Regalis.Infrastructure/Persistence/Migrations/.
    /// See README.md in this folder before applying — the entity properties and
    /// the model snapshot must be updated in the same change, or `dotnet ef`
    /// will generate a duplicate migration.
    /// </summary>
    [DbContext(typeof(RegalisDbContext))]
    [Migration("20260908000000_AddUserProfileFields")]
    public partial class AddUserProfileFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                table: "MST_User",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "MST_User",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "MST_User",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "MST_User",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "MST_User",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "MST_User",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // If the User entity declares this as DateTime? instead of DateOnly?,
            // change the generic argument to <DateTime>. The emitted DDL is the
            // same either way because the column type is stated explicitly.
            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "MST_User",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Gender",
                table: "MST_User",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gstin",
                table: "MST_User",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Gstin", table: "MST_User");
            migrationBuilder.DropColumn(name: "Gender", table: "MST_User");
            migrationBuilder.DropColumn(name: "DateOfBirth", table: "MST_User");
            migrationBuilder.DropColumn(name: "PostalCode", table: "MST_User");
            migrationBuilder.DropColumn(name: "Country", table: "MST_User");
            migrationBuilder.DropColumn(name: "State", table: "MST_User");
            migrationBuilder.DropColumn(name: "City", table: "MST_User");
            migrationBuilder.DropColumn(name: "AddressLine2", table: "MST_User");
            migrationBuilder.DropColumn(name: "AddressLine1", table: "MST_User");
        }
    }
}
