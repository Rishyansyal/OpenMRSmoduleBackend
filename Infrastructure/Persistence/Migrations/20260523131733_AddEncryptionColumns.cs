using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenMRSmoduleBackend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEncryptionColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bestaande rijen bevatten plain-text e-mailadressen die niet meer
            // bruikbaar zijn na de invoering van encryptie. Verwijder ze zodat
            // de unieke index op email_hash aangemaakt kan worden zonder conflicten.
            // In productie dienen gebruikers opnieuw te registreren.
            migrationBuilder.Sql("DELETE FROM users;");

            migrationBuilder.DropIndex(
                name: "IX_users_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_reminder_logs_encounter_id_reminder_window",
                table: "reminder_logs");

            migrationBuilder.AddColumn<string>(
                name: "email_hash",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "encounter_id_hash",
                table: "reminder_logs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_users_email_hash",
                table: "users",
                column: "email_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reminder_logs_encounter_id_hash_reminder_window",
                table: "reminder_logs",
                columns: new[] { "encounter_id_hash", "reminder_window" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_email_hash",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_reminder_logs_encounter_id_hash_reminder_window",
                table: "reminder_logs");

            migrationBuilder.DropColumn(
                name: "email_hash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "encounter_id_hash",
                table: "reminder_logs");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reminder_logs_encounter_id_reminder_window",
                table: "reminder_logs",
                columns: new[] { "encounter_id", "reminder_window" });
        }
    }
}
