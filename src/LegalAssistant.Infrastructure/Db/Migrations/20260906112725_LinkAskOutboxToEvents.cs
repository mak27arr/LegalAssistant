using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalAssistant.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class LinkAskOutboxToEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ask_job_event_id",
                table: "message_outbox",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_message_outbox_ask_job_event_id",
                table: "message_outbox",
                column: "ask_job_event_id");

            migrationBuilder.AddForeignKey(
                name: "FK_message_outbox_ask_job_events_ask_job_event_id",
                table: "message_outbox",
                column: "ask_job_event_id",
                principalTable: "ask_job_events",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_message_outbox_ask_job_events_ask_job_event_id",
                table: "message_outbox");

            migrationBuilder.DropIndex(
                name: "IX_message_outbox_ask_job_event_id",
                table: "message_outbox");

            migrationBuilder.DropColumn(
                name: "ask_job_event_id",
                table: "message_outbox");
        }
    }
}
