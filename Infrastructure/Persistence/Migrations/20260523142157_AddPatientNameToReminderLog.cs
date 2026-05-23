using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenMRSmoduleBackend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientNameToReminderLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "patient_name",
                table: "reminder_logs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "patient_name",
                table: "reminder_logs");
        }
    }
}
