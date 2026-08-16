using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalAssistant.Infrastructure.Db.Migrations;

public partial class AddDocumentChunkEmbeddingAnnIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS ix_document_chunks_embedding_hnsw
                ON document_chunks
                USING hnsw (embedding vector_l2_ops)
                WHERE embedding IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS ix_document_chunks_embedding_hnsw;
            """);
    }
}
