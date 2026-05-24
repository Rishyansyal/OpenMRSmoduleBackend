using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenMRSmoduleBackend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookAppointmentScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointment_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<string>(type: "text", nullable: false),
                    encounter_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    patient_id_encrypted = table.Column<string>(type: "text", nullable: false),
                    patient_display_encrypted = table.Column<string>(type: "text", nullable: true),
                    service_type_encrypted = table.Column<string>(type: "text", nullable: true),
                    location_encrypted = table.Column<string>(type: "text", nullable: true),
                    instructions_encrypted = table.Column<string>(type: "text", nullable: true),
                    last_event_id = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organization_integration_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<string>(type: "text", nullable: false),
                    default_provider = table.Column<string>(type: "text", nullable: false),
                    time_zone_id = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_integration_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_event_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    organization_id = table.Column<string>(type: "text", nullable: false),
                    resource_type = table.Column<string>(type: "text", nullable: false),
                    resource_id = table.Column<string>(type: "text", nullable: false),
                    payload_sha256 = table.Column<string>(type: "text", nullable: false),
                    duplicate = table.Column<bool>(type: "boolean", nullable: false),
                    processed = table.Column<bool>(type: "boolean", nullable: false),
                    error_code = table.Column<string>(type: "text", nullable: true),
                    event_timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_event_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_reminders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<string>(type: "text", nullable: false),
                    encounter_id = table.Column<string>(type: "text", nullable: false),
                    reminder_window = table.Column<string>(type: "text", nullable: false),
                    scheduled_for_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    last_error_code = table.Column<string>(type: "text", nullable: true),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_reminders", x => x.id);
                    table.ForeignKey(
                        name: "FK_scheduled_reminders_appointment_notifications",
                        column: x => x.appointment_notification_id,
                        principalTable: "appointment_notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_notifications_organization_id_encounter_id",
                table: "appointment_notifications",
                columns: new[] { "organization_id", "encounter_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_integration_configs_organization_id",
                table: "organization_integration_configs",
                column: "organization_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_reminders_appointment_notification_id_reminder_window",
                table: "scheduled_reminders",
                columns: new[] { "appointment_notification_id", "reminder_window" });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_reminders_status_scheduled_for_utc",
                table: "scheduled_reminders",
                columns: new[] { "status", "scheduled_for_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_event_logs_event_id",
                table: "webhook_event_logs",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_event_logs_received_at_utc",
                table: "webhook_event_logs",
                column: "received_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "organization_integration_configs");
            migrationBuilder.DropTable(name: "scheduled_reminders");
            migrationBuilder.DropTable(name: "webhook_event_logs");
            migrationBuilder.DropTable(name: "appointment_notifications");
        }
    }
}
