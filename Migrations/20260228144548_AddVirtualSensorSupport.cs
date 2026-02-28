using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grefurBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtualSensorSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiProvider",
                table: "GrefurDevices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CustomHeadersJson",
                table: "GrefurDevices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "GrefurDevices",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DeviceCategory",
                table: "GrefurDevices",
                type: "varchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FrontendHeader",
                table: "GrefurDevices",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextScheduledFetchUtc",
                table: "GrefurDevices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderConfigurationJson",
                table: "GrefurDevices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "VirtualSensorValues",
                columns: table => new
                {
                    ValueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DeviceId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KeyName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JsonPath = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Unit = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastValue = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualSensorValues", x => x.ValueId);
                    table.ForeignKey(
                        name: "FK_VirtualSensorValues_GrefurDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "GrefurDevices",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualSensorValues_DeviceId",
                table: "VirtualSensorValues",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VirtualSensorValues");

            migrationBuilder.DropColumn(
                name: "ApiProvider",
                table: "GrefurDevices");

            migrationBuilder.DropColumn(
                name: "CustomHeadersJson",
                table: "GrefurDevices");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "GrefurDevices");

            migrationBuilder.DropColumn(
                name: "DeviceCategory",
                table: "GrefurDevices");

            migrationBuilder.DropColumn(
                name: "FrontendHeader",
                table: "GrefurDevices");

            migrationBuilder.DropColumn(
                name: "NextScheduledFetchUtc",
                table: "GrefurDevices");

            migrationBuilder.DropColumn(
                name: "ProviderConfigurationJson",
                table: "GrefurDevices");
        }
    }
}
