using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Construction.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Spec 01 §4.3 / §5.3: one-step approval slice. <c>approval_decisions</c> is append-only by
    /// grants. Additive and forward-only.
    /// </summary>
    [DbContext(typeof(PlatformDbContext))]
    [Migration("20261121120000_AddApprovals")]
    public partial class AddApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS platform.approval_policies (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    code text NOT NULL,
                    record_type text NOT NULL,
                    min_amount numeric(18,4) NOT NULL DEFAULT 0,
                    max_amount numeric(18,4) NULL,
                    currency char(3) NOT NULL,
                    project_id uuid NULL,
                    trade text NULL,
                    priority int NOT NULL DEFAULT 100,
                    is_active boolean NOT NULL DEFAULT true,
                    effective_from date NOT NULL,
                    effective_to date NULL,
                    version int NOT NULL DEFAULT 1,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    CONSTRAINT ux_approval_policies_tenant_code UNIQUE (tenant_id, code),
                    CONSTRAINT ck_approval_policies_amounts
                        CHECK (max_amount IS NULL OR max_amount > min_amount)
                );

                CREATE TABLE IF NOT EXISTS platform.approval_policy_steps (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    policy_id uuid NOT NULL REFERENCES platform.approval_policies (id),
                    step_order int NOT NULL,
                    approver_source text NOT NULL,
                    approver_ids uuid[] NOT NULL,
                    limit_amount numeric(18,4) NULL,
                    CONSTRAINT ux_approval_policy_steps_order UNIQUE (policy_id, step_order)
                );

                CREATE TABLE IF NOT EXISTS platform.approval_instances (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    record_type text NOT NULL,
                    record_id uuid NOT NULL,
                    project_id uuid NULL,
                    requester_id uuid NOT NULL,
                    requested_at timestamptz NOT NULL DEFAULT now(),
                    status text NOT NULL DEFAULT 'pending'
                        CHECK (status IN ('pending', 'approved', 'rejected', 'returned', 'cancelled', 'superseded')),
                    policy_id uuid NULL,
                    policy_code text NOT NULL,
                    policy_version int NOT NULL,
                    amount numeric(18,4) NOT NULL,
                    currency char(3) NOT NULL,
                    context jsonb NULL,
                    row_version integer NOT NULL DEFAULT 1,
                    decided_at timestamptz NULL,
                    decided_by uuid NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ux_approval_instances_open
                    ON platform.approval_instances (tenant_id, record_type, record_id)
                    WHERE status = 'pending';

                CREATE TABLE IF NOT EXISTS platform.approval_steps (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    instance_id uuid NOT NULL REFERENCES platform.approval_instances (id),
                    step_order int NOT NULL,
                    status text NOT NULL DEFAULT 'pending'
                        CHECK (status IN ('pending', 'approved', 'rejected', 'returned')),
                    approver_ids uuid[] NOT NULL,
                    decided_by uuid NULL,
                    decided_at timestamptz NULL,
                    comment text NULL,
                    CONSTRAINT ux_approval_steps_order UNIQUE (instance_id, step_order)
                );

                CREATE TABLE IF NOT EXISTS platform.approval_decisions (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    instance_id uuid NOT NULL REFERENCES platform.approval_instances (id),
                    step_id uuid NOT NULL REFERENCES platform.approval_steps (id),
                    actor_id uuid NOT NULL,
                    action text NOT NULL CHECK (action IN ('approve', 'reject', 'return')),
                    comment text NULL,
                    decided_at timestamptz NOT NULL DEFAULT now()
                );

                CREATE INDEX IF NOT EXISTS ix_approval_decisions_instance
                    ON platform.approval_decisions (tenant_id, instance_id, decided_at);
                """);

            foreach (var table in new[]
                     {
                         "approval_policies", "approval_policy_steps", "approval_instances",
                         "approval_steps", "approval_decisions"
                     })
            {
                migrationBuilder.EnableTenantRowLevelSecurity("platform", table);
            }

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION platform.guard_approval_decisions()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'platform.approval_decisions is append-only';
                END;
                $$;

                DROP TRIGGER IF EXISTS trg_approval_decisions_append_only ON platform.approval_decisions;
                CREATE TRIGGER trg_approval_decisions_append_only
                    BEFORE UPDATE OR DELETE ON platform.approval_decisions
                    FOR EACH ROW EXECUTE FUNCTION platform.guard_approval_decisions();

                REVOKE UPDATE, DELETE, TRUNCATE ON platform.approval_decisions FROM app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS platform.approval_decisions;
                DROP TABLE IF EXISTS platform.approval_steps;
                DROP TABLE IF EXISTS platform.approval_instances;
                DROP TABLE IF EXISTS platform.approval_policy_steps;
                DROP TABLE IF EXISTS platform.approval_policies;
                DROP FUNCTION IF EXISTS platform.guard_approval_decisions();
                """);
        }
    }
}
