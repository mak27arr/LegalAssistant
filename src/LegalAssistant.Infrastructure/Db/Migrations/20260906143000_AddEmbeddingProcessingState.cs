using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalAssistant.Infrastructure.Db.Migrations;

public partial class AddEmbeddingProcessingState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "embedding_status",
            table: "document_chunks",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "Pending");

        migrationBuilder.AddColumn<int>(
            name: "embedding_attempt_count",
            table: "document_chunks",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "embedding_last_error",
            table: "document_chunks",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "embedding_started_at",
            table: "document_chunks",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "embedding_completed_at",
            table: "document_chunks",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "embedding_failed_at",
            table: "document_chunks",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "embedding_updated_at",
            table: "document_chunks",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "job_id",
            table: "document_chunks",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "status",
            table: "chunking_runs",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "InProgress");

        migrationBuilder.AddColumn<int>(
            name: "total_chunks",
            table: "chunking_runs",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "completed_chunks",
            table: "chunking_runs",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "failed_chunks",
            table: "chunking_runs",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "last_error",
            table: "chunking_runs",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "embedding_completed_at",
            table: "chunking_runs",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_document_chunks_job_id_embedding_status",
            table: "document_chunks",
            columns: new[] { "job_id", "embedding_status" });

        migrationBuilder.Sql(
            "UPDATE document_chunks SET embedding_status = 'Completed', embedding_completed_at = created_at WHERE embedding IS NOT NULL;");

        migrationBuilder.Sql(@"
            UPDATE chunking_runs AS runs
            SET total_chunks = summary.total_chunks,
                completed_chunks = summary.completed_chunks,
                failed_chunks = summary.failed_chunks,
                status = CASE
                    WHEN summary.failed_chunks > 0 THEN 'Failed'
                    WHEN summary.completed_chunks = summary.total_chunks THEN 'Completed'
                    ELSE 'EmbeddingInProgress'
                END,
                embedding_completed_at = CASE
                    WHEN summary.failed_chunks = 0 AND summary.completed_chunks = summary.total_chunks THEN runs.updated_at
                    ELSE NULL
                END
            FROM (
                SELECT chunking_run_id,
                       COUNT(*)::integer AS total_chunks,
                       COUNT(*) FILTER (WHERE embedding_status = 'Completed' AND embedding IS NOT NULL)::integer AS completed_chunks,
                       COUNT(*) FILTER (WHERE embedding_status = 'Failed')::integer AS failed_chunks
                FROM document_chunks
                WHERE chunking_run_id IS NOT NULL
                GROUP BY chunking_run_id
            ) AS summary
            WHERE runs.id = summary.chunking_run_id;");

        migrationBuilder.Sql(@"
            UPDATE document_chunks AS chunks
            SET job_id = (
                SELECT jobs.id
                FROM jobs
                WHERE jobs.type = 'ingest'
                  AND jobs.payload LIKE '%""DocumentId"":""' || chunks.document_id::text || '""%'
                ORDER BY jobs.created_at DESC
                LIMIT 1
            )
            WHERE chunks.job_id IS NULL
              AND EXISTS (
                  SELECT 1
                  FROM jobs
                  WHERE jobs.type = 'ingest'
                    AND jobs.payload LIKE '%""DocumentId"":""' || chunks.document_id::text || '""%'
              );");

        migrationBuilder.Sql(@"
            UPDATE jobs
            SET status = 'EmbeddingInProgress',
                result = json_build_object('embeddings', 'pending')::text,
                updated_at = now()
            WHERE type = 'ingest'
              AND status = 'Completed'
              AND EXISTS (
                  SELECT 1
                  FROM document_chunks
                  WHERE document_chunks.job_id = jobs.id
                    AND (document_chunks.embedding_status <> 'Completed' OR document_chunks.embedding IS NULL)
              );");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_document_chunks_job_id_embedding_status",
            table: "document_chunks");

        migrationBuilder.DropColumn(name: "embedding_status", table: "document_chunks");
        migrationBuilder.DropColumn(name: "embedding_attempt_count", table: "document_chunks");
        migrationBuilder.DropColumn(name: "embedding_last_error", table: "document_chunks");
        migrationBuilder.DropColumn(name: "embedding_started_at", table: "document_chunks");
        migrationBuilder.DropColumn(name: "embedding_completed_at", table: "document_chunks");
        migrationBuilder.DropColumn(name: "embedding_failed_at", table: "document_chunks");
        migrationBuilder.DropColumn(name: "embedding_updated_at", table: "document_chunks");
        migrationBuilder.DropColumn(name: "job_id", table: "document_chunks");
        migrationBuilder.DropColumn(name: "status", table: "chunking_runs");
        migrationBuilder.DropColumn(name: "total_chunks", table: "chunking_runs");
        migrationBuilder.DropColumn(name: "completed_chunks", table: "chunking_runs");
        migrationBuilder.DropColumn(name: "failed_chunks", table: "chunking_runs");
        migrationBuilder.DropColumn(name: "last_error", table: "chunking_runs");
        migrationBuilder.DropColumn(name: "embedding_completed_at", table: "chunking_runs");
    }
}
