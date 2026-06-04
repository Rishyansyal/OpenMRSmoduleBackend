using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenMRSmoduleBackend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderQueueEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "consumed_at_utc",
                table: "scheduled_reminders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "queue_message_id",
                table: "scheduled_reminders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "queued_at_utc",
                table: "scheduled_reminders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "consumed_at_utc",
                table: "scheduled_reminders");

            migrationBuilder.DropColumn(
                name: "queue_message_id",
                table: "scheduled_reminders");

            migrationBuilder.DropColumn(
                name: "queued_at_utc",
                table: "scheduled_reminders");
        }
    }
}
