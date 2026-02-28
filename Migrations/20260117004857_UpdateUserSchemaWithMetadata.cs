using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grefurBackend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserSchemaWithMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Role",
                table: "GrefurUsers",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "GrefurUsers",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "GrefurUsers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLogin",
                table: "GrefurUsers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "GrefurUsers",
                type: "json",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "GrefurUsers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "GrefurUsers");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "GrefurUsers");

            migrationBuilder.DropColumn(
                name: "LastLogin",
                table: "GrefurUsers");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "GrefurUsers");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "GrefurUsers");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "GrefurUsers",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
