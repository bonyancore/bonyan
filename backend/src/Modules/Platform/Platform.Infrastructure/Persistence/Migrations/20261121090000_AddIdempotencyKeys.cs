using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Construction.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Spec 01 §4.13: <c>platform.idempotency_keys</c>. Store-and-replay for creating/acting POSTs.
    /// Additive and forward-only.
    /// </summary>
    [DbContext(typeof(PlatformDbContext))]
    [Migration("20261121090000_AddIdempotencyKeys")]
    public partial class AddIdempotencyKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS platform.idempotency_keys (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    principal_type text NOT NULL CHECK (principal_type IN ('user', 'external_user')),
                    principal_id uuid NOT NULL,
                    idem_key text NOT NULL CHECK (char_length(idem_key) BETWEEN 8 AND 64),
                    http_method text NOT NULL,
                    path_template text NOT NULL,
                    request_hash bytea NOT NULL,
                    status text NOT NULL CHECK (status IN ('in_progress', 'completed')),
                    response_status int NULL,
                    response_headers jsonb NULL,
                    response_body bytea NULL,
                    replay_too_large boolean NOT NULL DEFAULT false,
                    expires_at timestamptz NOT NULL,
                    CONSTRAINT ux_idempotency_keys_scope UNIQUE (tenant_id, principal_id, idem_key),
                    CONSTRAINT ck_idempotency_keys_body_size
                        CHECK (response_body IS NULL OR octet_length(response_body) <= 262144)
                );

                CREATE INDEX IF NOT EXISTS ix_idempotency_keys_expires
                    ON platform.idempotency_keys (expires_at);
                """);

            migrationBuilder.EnableTenantRowLevelSecurity("platform", "idempotency_keys");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS platform.idempotency_keys;
                """);
        }
    }
}
