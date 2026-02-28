using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grefurBackend.Migrations
{
    /// <inheritdoc />
    public partial class addedDeviceIPData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpectedServerCertificateThumbprint",
                table: "GrefurDevices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RemoteAddress",
                table: "GrefurDevices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ServiceValidationToken",
                table: "GrefurDevices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedServerCertificateThumbprint",
                table: "GrefurDevices");

            migrationBuilder.DropColumn(
                name: "RemoteAddress",
                table: "GrefurDevices");

            migrationBuilder.DropColumn(
                name: "ServiceValidationToken",
                table: "GrefurDevices");
        }
    }
}
