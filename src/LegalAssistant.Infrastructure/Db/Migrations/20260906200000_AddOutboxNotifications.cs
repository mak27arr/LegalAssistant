using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalAssistant.Infrastructure.Db.Migrations;

public partial class AddOutboxNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION notify_legalassistant_outbox_insert()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                PERFORM pg_notify('legalassistant_outbox', NEW.id::text);
                RETURN NEW;
            END;
            $$;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER message_outbox_notify_insert
            AFTER INSERT ON message_outbox
            FOR EACH ROW
            EXECUTE FUNCTION notify_legalassistant_outbox_insert();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS message_outbox_notify_insert ON message_outbox;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS notify_legalassistant_outbox_insert();");
    }
}
