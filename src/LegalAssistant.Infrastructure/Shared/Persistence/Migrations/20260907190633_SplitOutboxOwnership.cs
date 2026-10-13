using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalAssistant.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class SplitOutboxOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "owner",
                table: "message_outbox",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "worker-ingest");

            migrationBuilder.Sql("""
                UPDATE message_outbox
                SET owner = 'worker-ask'
                WHERE message_type LIKE 'ask.job.%';

                UPDATE message_outbox
                SET owner = 'embeddings'
                WHERE message_type = 'embedding.completed';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_message_outbox_owner_status_next_attempt_at_created_at",
                table: "message_outbox",
                columns: new[] { "owner", "status", "next_attempt_at", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_message_outbox_owner_status_next_attempt_at_created_at",
                table: "message_outbox");

            migrationBuilder.DropColumn(
                name: "owner",
                table: "message_outbox");
        }
    }
}
