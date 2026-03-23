using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalAssistant.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkingRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "active_chunking_run_id",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "chunking_run_id",
                table: "document_chunks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "chunking_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_name = table.Column<string>(type: "text", nullable: false),
                    strategy_version = table.Column<string>(type: "text", nullable: false),
                    params_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunking_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chunking_runs_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_chunking_run_id",
                table: "document_chunks",
                column: "chunking_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_chunking_runs_document_id_created_at",
                table: "chunking_runs",
                columns: new[] { "document_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_document_chunks_chunking_runs_chunking_run_id",
                table: "document_chunks",
                column: "chunking_run_id",
                principalTable: "chunking_runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_document_chunks_chunking_runs_chunking_run_id",
                table: "document_chunks");

            migrationBuilder.DropTable(
                name: "chunking_runs");

            migrationBuilder.DropIndex(
                name: "IX_document_chunks_chunking_run_id",
                table: "document_chunks");

            migrationBuilder.DropColumn(
                name: "active_chunking_run_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "chunking_run_id",
                table: "document_chunks");
        }
    }
}
