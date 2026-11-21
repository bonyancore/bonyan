using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Construction.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// CC-05: minimal <c>platform.audit_log</c> used by PLT-206/207/208/209. Append-only by grants,
    /// with a defensive trigger. Hash chaining and the search API remain PLT-109.
    /// </summary>
    [DbContext(typeof(PlatformDbContext))]
    [Migration("20261121093000_AddAuditLog")]
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS platform.audit_log (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    at timestamptz NOT NULL DEFAULT now(),
                    user_id uuid NULL,
                    entity_type text NOT NULL,
                    entity_id uuid NULL,
                    action text NOT NULL,
                    before jsonb NULL,
                    after jsonb NULL,
                    reason text NULL,
                    ip_address text NULL,
                    correlation_id uuid NULL
                );

                CREATE INDEX IF NOT EXISTS ix_audit_log_tenant_entity
                    ON platform.audit_log (tenant_id, entity_type, entity_id, at DESC);
                """);

            migrationBuilder.EnableTenantRowLevelSecurity("platform", "audit_log");

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION platform.guard_audit_log()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'platform.audit_log is append-only';
                END;
                $$;

                DROP TRIGGER IF EXISTS trg_audit_log_append_only ON platform.audit_log;
                CREATE TRIGGER trg_audit_log_append_only
                    BEFORE UPDATE OR DELETE ON platform.audit_log
                    FOR EACH ROW EXECUTE FUNCTION platform.guard_audit_log();

                REVOKE UPDATE, DELETE, TRUNCATE ON platform.audit_log FROM app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS platform.audit_log;
                DROP FUNCTION IF EXISTS platform.guard_audit_log();
                """);
        }
    }
}
