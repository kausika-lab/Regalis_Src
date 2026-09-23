using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileFields : Migration
    {
        /// <inheritdoc />
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
                name: "Country",
                table: "MST_User",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

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

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "MST_User",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "MST_User",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "MST_User");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "MST_User");

            migrationBuilder.DropColumn(
                name: "City",
                table: "MST_User");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "MST_User");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "MST_User");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "MST_User");

            migrationBuilder.DropColumn(
                name: "Gstin",
                table: "MST_User");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "MST_User");

            migrationBuilder.DropColumn(
                name: "State",
                table: "MST_User");
        }
    }
}
