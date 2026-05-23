using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenMRSmoduleBackend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "message_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    message_type = table.Column<string>(type: "text", nullable: false),
                    recipient_count = table.Column<int>(type: "integer", nullable: false),
                    failed_count = table.Column<int>(type: "integer", nullable: false),
                    provider_message_id = table.Column<string>(type: "text", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_code = table.Column<string>(type: "text", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_by_user_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_message_logs_sent_at",
                table: "message_logs",
                column: "sent_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "message_logs");
        }
    }
}
