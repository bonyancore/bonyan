using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Construction.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Spec 01 §4.9: <c>number_series</c>, <c>number_series_counters</c> and the append-only
    /// <c>number_allocations</c>. Additive and forward-only.
    /// </summary>
    [DbContext(typeof(PlatformDbContext))]
    [Migration("20261121100000_AddNumberSeries")]
    public partial class AddNumberSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS platform.number_series (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    code text NOT NULL,
                    name_ar text NULL,
                    name_en text NULL,
                    prefix text NOT NULL,
                    padding int NOT NULL DEFAULT 4 CHECK (padding BETWEEN 1 AND 10),
                    scope text NOT NULL DEFAULT 'tenant'
                        CHECK (scope IN ('tenant', 'project', 'year', 'project_year')),
                    format text NOT NULL DEFAULT '{prefix}{seq}',
                    start_value bigint NOT NULL DEFAULT 1,
                    step int NOT NULL DEFAULT 1 CHECK (step > 0),
                    is_locked boolean NOT NULL DEFAULT false,
                    row_version integer NOT NULL DEFAULT 1,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    updated_at timestamptz NULL,
                    CONSTRAINT ux_number_series_tenant_code UNIQUE (tenant_id, code)
                );

                CREATE TABLE IF NOT EXISTS platform.number_series_counters (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    series_id uuid NOT NULL REFERENCES platform.number_series (id),
                    scope_key text NOT NULL,
                    next_value bigint NOT NULL,
                    CONSTRAINT ux_number_series_counters_scope UNIQUE (tenant_id, series_id, scope_key)
                );

                CREATE TABLE IF NOT EXISTS platform.number_allocations (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    series_id uuid NOT NULL REFERENCES platform.number_series (id),
                    scope_key text NOT NULL,
                    seq_value bigint NOT NULL,
                    number text NOT NULL,
                    entity_type text NULL,
                    entity_id uuid NULL,
                    allocated_at timestamptz NOT NULL DEFAULT now(),
                    CONSTRAINT ux_number_allocations_number UNIQUE (tenant_id, series_id, number)
                );

                CREATE INDEX IF NOT EXISTS ix_number_allocations_series_allocated
                    ON platform.number_allocations (tenant_id, series_id, allocated_at DESC, id DESC);
                """);

            migrationBuilder.EnableTenantRowLevelSecurity("platform", "number_series");
            migrationBuilder.EnableTenantRowLevelSecurity("platform", "number_series_counters");
            migrationBuilder.EnableTenantRowLevelSecurity("platform", "number_allocations");

            // CC-02: issued numbers are append-only by grants. The only permitted update is the
            // one-time attach of the owning entity, through a SECURITY DEFINER function.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION platform.guard_number_allocations()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'platform.number_allocations is append-only';
                    END IF;

                    IF NEW.id <> OLD.id
                       OR NEW.tenant_id <> OLD.tenant_id
                       OR NEW.series_id <> OLD.series_id
                       OR NEW.scope_key <> OLD.scope_key
                       OR NEW.seq_value <> OLD.seq_value
                       OR NEW.number <> OLD.number
                       OR NEW.allocated_at <> OLD.allocated_at
                       OR OLD.entity_type IS NOT NULL THEN
                        RAISE EXCEPTION 'platform.number_allocations is append-only';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                DROP TRIGGER IF EXISTS trg_number_allocations_append_only ON platform.number_allocations;
                CREATE TRIGGER trg_number_allocations_append_only
                    BEFORE UPDATE OR DELETE ON platform.number_allocations
                    FOR EACH ROW EXECUTE FUNCTION platform.guard_number_allocations();

                CREATE OR REPLACE FUNCTION platform.attach_number_allocation(
                    p_id uuid, p_entity_type text, p_entity_id uuid)
                RETURNS boolean
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = platform, pg_temp
                AS $$
                BEGIN
                    UPDATE platform.number_allocations
                    SET entity_type = p_entity_type,
                        entity_id = p_entity_id
                    WHERE id = p_id AND entity_type IS NULL;
                    RETURN FOUND;
                END;
                $$;

                REVOKE ALL ON FUNCTION platform.attach_number_allocation(uuid, text, uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION platform.attach_number_allocation(uuid, text, uuid) TO app;
                REVOKE UPDATE, DELETE, TRUNCATE ON platform.number_allocations FROM app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS platform.number_allocations;
                DROP TABLE IF EXISTS platform.number_series_counters;
                DROP TABLE IF EXISTS platform.number_series;
                DROP FUNCTION IF EXISTS platform.attach_number_allocation(uuid, text, uuid);
                DROP FUNCTION IF EXISTS platform.guard_number_allocations();
                """);
        }
    }
}
