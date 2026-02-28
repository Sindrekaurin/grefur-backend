using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grefurBackend.Migrations.Timescale
{
    /// <inheritdoc />
    public partial class recreatedTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_sensorReadings",
                table: "sensorReadings");

            migrationBuilder.AddPrimaryKey(
                name: "PK_sensorReadings",
                table: "sensorReadings",
                columns: new[] { "timestamp", "Topic", "customerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_sensorReadings",
                table: "sensorReadings");

            migrationBuilder.AddPrimaryKey(
                name: "PK_sensorReadings",
                table: "sensorReadings",
                columns: new[] { "timestamp", "deviceId", "property" });
        }
    }
}
