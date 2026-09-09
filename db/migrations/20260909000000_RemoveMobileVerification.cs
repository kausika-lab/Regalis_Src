using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Drops MST_User.IsMobileVerified. Mobile verification was never wired up:
    /// no endpoint sets it, every TRN_Otp row is Channel 1 / Purpose 1 (email /
    /// registration), and all existing users sit at IsMobileVerified = 0, so the
    /// admin UI could only ever render "Unverified". No index, no default
    /// constraint and no foreign key referenced the column.
    ///
    /// ORDER OF DEPLOYMENT MATTERS — read README.md in this folder first.
    /// EF Core emits explicit column lists, so while User.IsMobileVerified is
    /// still in the model every SELECT against MST_User names this column. Drop
    /// it before the API stops mapping it and every user query fails with
    /// "Invalid column name 'IsMobileVerified'", which takes down login, the
    /// profile and all admin user screens.
    ///
    /// Correct sequence: remove the property, its EF configuration and the
    /// UserDetailDto field -> deploy the API -> run this migration.
    ///
    /// Nothing else changes. IsEmailVerified stays: email OTP verification is
    /// live and in use. TRN_Otp needs no change — its Channel column carries the
    /// email channel too.
    /// </summary>
    [DbContext(typeof(RegalisDbContext))]
    [Migration("20260909000000_RemoveMobileVerification")]
    public partial class RemoveMobileVerification : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMobileVerified",
                table: "MST_User");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The original column was bit NOT NULL with no default constraint.
            // A default is required to re-add a non-nullable column to a table
            // that already holds rows; EF names and keeps that constraint, which
            // is the one difference from the original shape. The .sql rollback
            // script in this folder drops the helper constraint afterwards if
            // you need the column restored exactly as it was.
            migrationBuilder.AddColumn<bool>(
                name: "IsMobileVerified",
                table: "MST_User",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
