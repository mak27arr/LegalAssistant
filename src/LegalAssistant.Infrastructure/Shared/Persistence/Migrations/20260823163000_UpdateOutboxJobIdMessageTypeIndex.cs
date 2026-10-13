using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalAssistant.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOutboxJobIdMessageTypeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_message_outbox_job_id",
                table: "message_outbox");

            migrationBuilder.CreateIndex(
                name: "IX_message_outbox_job_id_message_type",
                table: "message_outbox",
                columns: new[] { "job_id", "message_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_message_outbox_job_id_message_type",
                table: "message_outbox");

            migrationBuilder.CreateIndex(
                name: "IX_message_outbox_job_id",
                table: "message_outbox",
                column: "job_id",
                unique: true);
        }
    }
}
