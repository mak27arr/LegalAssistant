using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalAssistant.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_message_outbox_job_id_message_type",
                table: "message_outbox");

            migrationBuilder.DropIndex(
                name: "IX_document_chunks_chunking_run_id",
                table: "document_chunks");

            migrationBuilder.AlterColumn<Guid>(
                name: "job_id",
                table: "message_outbox",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "deduplication_key",
                table: "message_outbox",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "job_id",
                table: "chunking_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE message_outbox
                SET deduplication_key = job_id::text || ':' || message_type
                WHERE deduplication_key IS NULL AND job_id IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_message_outbox_job_id_message_type",
                table: "message_outbox",
                columns: new[] { "job_id", "message_type" });

            migrationBuilder.CreateIndex(
                name: "IX_message_outbox_message_type_deduplication_key",
                table: "message_outbox",
                columns: new[] { "message_type", "deduplication_key" },
                unique: true,
                filter: "\"deduplication_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_chunking_run_id_chunk_index",
                table: "document_chunks",
                columns: new[] { "chunking_run_id", "chunk_index" },
                unique: true,
                filter: "\"chunking_run_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_chunking_runs_job_id",
                table: "chunking_runs",
                column: "job_id",
                unique: true,
                filter: "\"job_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_message_outbox_job_id_message_type",
                table: "message_outbox");

            migrationBuilder.DropIndex(
                name: "IX_message_outbox_message_type_deduplication_key",
                table: "message_outbox");

            migrationBuilder.DropIndex(
                name: "IX_document_chunks_chunking_run_id_chunk_index",
                table: "document_chunks");

            migrationBuilder.DropIndex(
                name: "IX_chunking_runs_job_id",
                table: "chunking_runs");

            migrationBuilder.DropColumn(
                name: "deduplication_key",
                table: "message_outbox");

            migrationBuilder.DropColumn(
                name: "job_id",
                table: "chunking_runs");

            migrationBuilder.AlterColumn<Guid>(
                name: "job_id",
                table: "message_outbox",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_message_outbox_job_id_message_type",
                table: "message_outbox",
                columns: new[] { "job_id", "message_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_chunking_run_id",
                table: "document_chunks",
                column: "chunking_run_id");
        }
    }
}
