using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grefurBackend.Migrations.Timescale
{
    /// <inheritdoc />
    public partial class InitialSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sensorReadings",
                columns: table => new
                {
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deviceId = table.Column<string>(type: "text", nullable: false),
                    property = table.Column<string>(type: "text", nullable: false),
                    customerId = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensorReadings", x => new { x.timestamp, x.deviceId, x.property });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sensorReadings");
        }
    }
}
