using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalAssistant.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddAskJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ask_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_scope_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    top_k = table.Column<int>(type: "integer", nullable: false),
                    conversation_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    request_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    result_json = table.Column<string>(type: "text", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ask_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ask_jobs_actor_scope_key_idempotency_key",
                table: "ask_jobs",
                columns: new[] { "actor_scope_key", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ask_jobs_status_created_at",
                table: "ask_jobs",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ask_jobs");
        }
    }
}
