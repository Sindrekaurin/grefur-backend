using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grefurBackend.Migrations
{
    /// <inheritdoc />
    public partial class addedMlModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MlAlarmConfigurations",
                columns: table => new
                {
                    MlAlarmId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomerId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetMeasurementId = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FeatureMeasurementIds = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FeatureOrder = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeviationProbabilityThreshold = table.Column<double>(type: "double", nullable: false),
                    TrainingFrequency = table.Column<int>(type: "int", nullable: false),
                    SampleIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    ModelVersion = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastTrainedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModelUri = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MlAlarmConfigurations", x => x.MlAlarmId);
                    table.ForeignKey(
                        name: "FK_MlAlarmConfigurations_GrefurCustomers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "GrefurCustomers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MlAlarmConfigurations_CustomerId",
                table: "MlAlarmConfigurations",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MlAlarmConfigurations");
        }
    }
}
