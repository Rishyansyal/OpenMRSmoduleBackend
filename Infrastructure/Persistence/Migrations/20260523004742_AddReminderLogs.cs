using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenMRSmoduleBackend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reminder_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_id = table.Column<string>(type: "text", nullable: false),
                    reminder_window = table.Column<string>(type: "text", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_code = table.Column<string>(type: "text", nullable: true),
                    encounter_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminder_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reminder_logs_encounter_id_reminder_window",
                table: "reminder_logs",
                columns: new[] { "encounter_id", "reminder_window" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reminder_logs");
        }
    }
}
