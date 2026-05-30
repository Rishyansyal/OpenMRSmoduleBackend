using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenMRSmoduleBackend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiOrganizationReliability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "scheduled_reminders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_attempt_at_utc",
                table: "scheduled_reminders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_attempts",
                table: "scheduled_reminders",
                type: "integer",
                nullable: false,
                defaultValue: 288);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_attempt_at_utc",
                table: "scheduled_reminders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_message_id",
                table: "scheduled_reminders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retry_base_delay_seconds",
                table: "scheduled_reminders",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "retry_max_delay_minutes",
                table: "scheduled_reminders",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<bool>(
                name: "enabled",
                table: "organization_integration_configs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "max_delivery_attempts",
                table: "organization_integration_configs",
                type: "integer",
                nullable: false,
                defaultValue: 288);

            migrationBuilder.AddColumn<string>(
                name: "openmrs_base_url",
                table: "organization_integration_configs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "openmrs_password_encrypted",
                table: "organization_integration_configs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "openmrs_username_encrypted",
                table: "organization_integration_configs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "poller_enabled",
                table: "organization_integration_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "poller_interval_minutes",
                table: "organization_integration_configs",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "poller_lookahead_hours",
                table: "organization_integration_configs",
                type: "integer",
                nullable: false,
                defaultValue: 48);

            migrationBuilder.AddColumn<int>(
                name: "retry_base_delay_seconds",
                table: "organization_integration_configs",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "retry_max_delay_minutes",
                table: "organization_integration_configs",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<string>(
                name: "webhook_secret_encrypted",
                table: "organization_integration_configs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "organization_provider_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<string>(type: "text", nullable: false),
                    provider_name = table.Column<string>(type: "text", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    base_url = table.Column<string>(type: "text", nullable: false),
                    student_group = table.Column<string>(type: "text", nullable: false),
                    credentials_json_encrypted = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_provider_configs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_reminders_status_next_attempt_at_utc",
                table: "scheduled_reminders",
                columns: new[] { "status", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_provider_configs_organization_id_provider_name",
                table: "organization_provider_configs",
                columns: new[] { "organization_id", "provider_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_provider_configs");

            migrationBuilder.DropIndex(
                name: "IX_scheduled_reminders_status_next_attempt_at_utc",
                table: "scheduled_reminders");

            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "scheduled_reminders");

            migrationBuilder.DropColumn(
                name: "last_attempt_at_utc",
                table: "scheduled_reminders");

            migrationBuilder.DropColumn(
                name: "max_attempts",
                table: "scheduled_reminders");

            migrationBuilder.DropColumn(
                name: "next_attempt_at_utc",
                table: "scheduled_reminders");

            migrationBuilder.DropColumn(
                name: "provider_message_id",
                table: "scheduled_reminders");

            migrationBuilder.DropColumn(
                name: "retry_base_delay_seconds",
                table: "scheduled_reminders");

            migrationBuilder.DropColumn(
                name: "retry_max_delay_minutes",
                table: "scheduled_reminders");

            migrationBuilder.DropColumn(
                name: "enabled",
                table: "organization_integration_configs");

            migrationBuilder.DropColumn(
                name: "max_delivery_attempts",
                table: "organization_integration_configs");

            migrationBuilder.DropColumn(
                name: "openmrs_base_url",
                table: "organization_integration_configs");

            migrationBuilder.DropColumn(
                name: "openmrs_password_encrypted",
                table: "organization_integration_configs");

            migrationBuilder.DropColumn(
                name: "openmrs_username_encrypted",
                table: "organization_integration_configs");

            migrationBuilder.DropColumn(
                name: "poller_enabled",
                table: "organization_integration_configs");

            migrationBuilder.DropColumn(
                name: "poller_interval_minutes",
                table: "organization_integration_configs");

            migrationBuilder.DropColumn(
                name: "poller_lookahead_hours",
                table: "organization_integration_configs");

            migrationBuilder.DropColumn(
                name: "retry_base_delay_seconds",
                table: "organization_integration_configs");

            migrationBuilder.DropColumn(
                name: "retry_max_delay_minutes",
                table: "organization_integration_configs");

            migrationBuilder.DropColumn(
                name: "webhook_secret_encrypted",
                table: "organization_integration_configs");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    email_hash = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_email_hash",
                table: "users",
                column: "email_hash",
                unique: true);
        }
    }
}
