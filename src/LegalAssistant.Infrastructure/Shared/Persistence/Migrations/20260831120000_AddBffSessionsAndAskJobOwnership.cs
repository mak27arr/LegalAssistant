using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LegalAssistant.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddBffSessionsAndAskJobOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auth_sessions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_renewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_auth_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    xml = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_protection_keys", x => x.id);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                table: "ask_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("UPDATE ask_jobs SET owner_user_id = NULL");

            migrationBuilder.DropIndex(
                name: "IX_ask_jobs_actor_scope_key_idempotency_key",
                table: "ask_jobs");

            migrationBuilder.CreateIndex(
                name: "IX_ask_jobs_owner_user_id_idempotency_key",
                table: "ask_jobs",
                columns: new[] { "owner_user_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_expires_at",
                table: "auth_sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_user_id",
                table: "auth_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_protection_keys_friendly_name",
                table: "data_protection_keys",
                column: "friendly_name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ask_jobs_users_owner_user_id",
                table: "ask_jobs",
                column: "owner_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ask_jobs_users_owner_user_id",
                table: "ask_jobs");

            migrationBuilder.DropTable(
                name: "auth_sessions");

            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropIndex(
                name: "IX_ask_jobs_owner_user_id_idempotency_key",
                table: "ask_jobs");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "ask_jobs");

            migrationBuilder.CreateIndex(
                name: "IX_ask_jobs_actor_scope_key_idempotency_key",
                table: "ask_jobs",
                columns: new[] { "actor_scope_key", "idempotency_key" },
                unique: true);
        }
    }
}
