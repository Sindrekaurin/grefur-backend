using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grefurBackend.Migrations
{
    /// <inheritdoc />
    public partial class ForcedPasswordChangeSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ForceAdminPasswordChange",
                table: "GrefurUsers",
                newName: "ForcePasswordChange");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ForcePasswordChange",
                table: "GrefurUsers",
                newName: "ForceAdminPasswordChange");
        }
    }
}
