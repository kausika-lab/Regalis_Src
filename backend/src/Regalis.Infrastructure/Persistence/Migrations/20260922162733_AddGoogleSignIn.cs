using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleSignIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_User_Mobile",
                table: "MST_User");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "MST_User",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(400)",
                oldMaxLength: 400);

            migrationBuilder.AddColumn<string>(
                name: "GoogleId",
                table: "MST_User",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_User_GoogleId",
                table: "MST_User",
                column: "GoogleId",
                unique: true,
                filter: "[GoogleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_User_Mobile",
                table: "MST_User",
                column: "MobileNumber",
                unique: true,
                filter: "[IsDeleted] = 0 AND [MobileNumber] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_User_GoogleId",
                table: "MST_User");

            migrationBuilder.DropIndex(
                name: "UX_User_Mobile",
                table: "MST_User");

            migrationBuilder.DropColumn(
                name: "GoogleId",
                table: "MST_User");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "MST_User",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(400)",
                oldMaxLength: 400,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_User_Mobile",
                table: "MST_User",
                column: "MobileNumber",
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
