using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grefurBackend.Migrations.Timescale
{
    /// <inheritdoc />
    public partial class AddedTopicField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "sensorReadings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Topic",
                table: "sensorReadings");
        }
    }
}
