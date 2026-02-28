using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grefurBackend.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedCustomerModelSubscriptionStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LoggedPointsUsage_LastMonth",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "LoggedPointsUsage_LastQuarter",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "LoggedPointsUsage_LastYear",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "LoggedPointsUsage_ThisMonth",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "LoggedPointsUsage_Total",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "NotificationUsage_Email_LastMonth",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "NotificationUsage_Email_LastQuarter",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "NotificationUsage_Email_LastYear",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "NotificationUsage_Email_ThisMonth",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "NotificationUsage_Email_Total",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "NotificationUsage_Sms_LastMonth",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "NotificationUsage_Sms_LastQuarter",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "NotificationUsage_Sms_LastYear",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "NotificationUsage_Sms_ThisMonth",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "NotificationUsage_Sms_Total",
                table: "GrefurCustomers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LoggedPointsUsage_LastMonth",
                table: "GrefurCustomers");

            migrationBuilder.DropColumn(
                name: "LoggedPointsUsage_LastQuarter",
                table: "GrefurCustomers");

            migrationBuilder.DropColumn(
                name: "LoggedPointsUsage_LastYear",
                table: "GrefurCustomers");

            migrationBuilder.DropColumn(
                name: "LoggedPointsUsage_ThisMonth",
                table: "GrefurCustomers");

            migrationBuilder.DropColumn(
                name: "LoggedPointsUsage_Total",
                table: "GrefurCustomers");

            migrationBuilder.DropColumn(
                name: "NotificationUsage_Email_LastMonth",
                table: "GrefurCustomers");

            migrationBuilder.DropColumn(
                name: "NotificationUsage_Email_LastQuarter",
                table: "GrefurCustomers");

            migrationBuilder.DropColumn(
                name: "NotificationUsage_Email_LastYear",
                table: "GrefurCustomers");

            migrationBuilder.DropColumn(
                name: "NotificationUsage_Email_ThisMonth",
                table: "GrefurCustomers");

            migrationBuilder.DropColumn(
                name: "NotificationUsage_Email_Total",
                table: "GrefurCustomers");

            migrationBuilder.DropColumn(
                name: "NotificationUsage_Sms_LastMonth",
                table: "GrefurCustomers");

            migrationBuilder.DropColumn(
                name: "NotificationUsage_Sms_LastQuarter",
                table: "GrefurCustomers");

            migrationBuilder.DropColumn(
                name: "NotificationUsage_Sms_LastYear",
                table: "GrefurCustomers");

            migrationBuilder.DropColumn(
                name: "NotificationUsage_Sms_ThisMonth",
                table: "GrefurCustomers");

            migrationBuilder.DropColumn(
                name: "NotificationUsage_Sms_Total",
                table: "GrefurCustomers");
        }
    }
}
