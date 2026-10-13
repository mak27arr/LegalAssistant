using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalAssistant.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrentProcessingLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "lease_id",
                table: "message_outbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "ask_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "lease_expires_at",
                table: "ask_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lease_id",
                table: "ask_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "started_at",
                table: "ask_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ask_jobs_status_lease_expires_at_created_at",
                table: "ask_jobs",
                columns: new[] { "status", "lease_expires_at", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ask_jobs_status_lease_expires_at_created_at",
                table: "ask_jobs");

            migrationBuilder.DropColumn(
                name: "lease_id",
                table: "message_outbox");

            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "ask_jobs");

            migrationBuilder.DropColumn(
                name: "lease_expires_at",
                table: "ask_jobs");

            migrationBuilder.DropColumn(
                name: "lease_id",
                table: "ask_jobs");

            migrationBuilder.DropColumn(
                name: "started_at",
                table: "ask_jobs");
        }
    }
}
