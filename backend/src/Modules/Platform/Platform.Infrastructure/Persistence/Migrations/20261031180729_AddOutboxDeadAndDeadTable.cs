using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Platform.Infrastructure.Persistence.Migrations;

    [Migration("20261031180729_AddOutboxDeadAndDeadTable")]
public partial class AddOutboxDeadAndDeadTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            @"CREATE TABLE IF NOT EXISTS platform.outbox_dead (
                id uuid PRIMARY KEY,
                original_event_id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                type text NOT NULL,
                payload jsonb NOT NULL,
                aggregate_type text NOT NULL,
                aggregate_id uuid NOT NULL,
                aggregate_version bigint NOT NULL,
                attempts int NOT NULL,
                last_error text NULL,
                last_error_category text NULL,
                dead_at timestamptz NOT NULL,
                replayed_at timestamptz NULL,
                replayed_by uuid NULL,
                replay_reason text NULL,
                CONSTRAINT uq_outbox_dead_original_event UNIQUE (original_event_id)
              );
              CREATE INDEX IF NOT EXISTS ix_outbox_dead_tenant_dead
                ON platform.outbox_dead (tenant_id, dead_at);
              ALTER TABLE platform.outbox_dead ENABLE ROW LEVEL SECURITY;
              ALTER TABLE platform.outbox_dead FORCE ROW LEVEL SECURITY;
              CREATE POLICY tenant_isolation
                ON platform.outbox_dead
                FOR ALL
                USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS platform.outbox_dead;");
    }
}
