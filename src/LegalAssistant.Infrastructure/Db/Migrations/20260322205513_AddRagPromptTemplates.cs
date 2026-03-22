using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalAssistant.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddRagPromptTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rag_prompt_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    system_header = table.Column<string>(type: "text", nullable: false),
                    instructions_footer = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_prompt_templates", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "rag_prompt_templates",
                columns: new[] { "Id", "system_header", "instructions_footer", "created_at", "updated_at" },
                values: new object[]
                {
                    new Guid("5d7fc428-f0cb-42a8-bbfe-6c9c0cf7e866"),
                    "Ти юридичний асистент. Відповідай українською. Якщо інформації в джерелах недостатньо — скажи про це.",
                    "- Дай коротку відповідь + деталізацію пунктами.\n- Додай посилання на джерела у вигляді [1], [2] де доречно.",
                    DateTime.UtcNow,
                    DateTime.UtcNow
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rag_prompt_templates");
        }
    }
}
