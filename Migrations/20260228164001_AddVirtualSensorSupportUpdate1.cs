using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grefurBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtualSensorSupportUpdate1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataType",
                table: "VirtualSensorValues",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<double>(
                name: "Multiplier",
                table: "VirtualSensorValues",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "ApiProviderUrl",
                table: "GrefurDevices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RequestBodyJson",
                table: "GrefurDevices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RequestHeadersJson",
                table: "GrefurDevices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataType",
                table: "VirtualSensorValues");

            migrationBuilder.DropColumn(
                name: "Multiplier",
                table: "VirtualSensorValues");

            migrationBuilder.DropColumn(
                name: "ApiProviderUrl",
                table: "GrefurDevices");

            migrationBuilder.DropColumn(
                name: "RequestBodyJson",
                table: "GrefurDevices");

            migrationBuilder.DropColumn(
                name: "RequestHeadersJson",
                table: "GrefurDevices");
        }
    }
}
