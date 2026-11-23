using System;
using Construction.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Spec 01 §4.2: platform.users and platform.refresh_tokens with RLS, composite constraints,
    /// and SECURITY DEFINER lookup functions.
    /// </summary>
    public partial class AddUsersAndRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Ensure citext extension exists
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS citext SCHEMA public;");

            // 2. Create platform.users table
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS platform.users (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    email public.citext NOT NULL,
                    phone text NULL,
                    full_name_ar text NULL,
                    full_name_en text NULL,
                    job_title text NULL,
                    status text NOT NULL DEFAULT 'invited',
                    password_hash text NULL,
                    password_changed_at timestamptz NULL,
                    failed_login_count int NOT NULL DEFAULT 0,
                    failed_login_window_started_at timestamptz NULL,
                    locked_until timestamptz NULL,
                    last_login_at timestamptz NULL,
                    mfa_enabled boolean NOT NULL DEFAULT false,
                    mfa_secret_enc bytea NULL,
                    mfa_recovery_hashes text NULL,
                    locale text NOT NULL DEFAULT 'ar',
                    numerals text NOT NULL DEFAULT 'latin',
                    calendar_pref text NOT NULL DEFAULT 'gregorian',
                    time_zone text NULL,
                    avatar_file_id uuid NULL,
                    signature_file_id uuid NULL,
                    manager_user_id uuid NULL,
                    deactivated_at timestamptz NULL,
                    deactivated_by uuid NULL,
                    deactivation_reason text NULL,
                    created_at timestamp NOT NULL DEFAULT now(),
                    created_by uuid NULL,
                    updated_at timestamp NOT NULL DEFAULT now(),
                    updated_by uuid NULL,
                    CONSTRAINT ck_users_status CHECK (status IN ('invited', 'active', 'deactivated', 'locked')),
                    CONSTRAINT ck_users_active_password CHECK (status <> 'active' OR password_hash IS NOT NULL),
                    CONSTRAINT ck_users_failed_login_count CHECK (failed_login_count >= 0),
                    CONSTRAINT ux_users_email UNIQUE (email),
                    CONSTRAINT ux_users_tenant_id_id UNIQUE (tenant_id, id)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_users_tenant_phone ON platform.users (tenant_id, phone) WHERE phone IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_users_tenant_status ON platform.users (tenant_id, status);
                CREATE INDEX IF NOT EXISTS ix_users_tenant_manager ON platform.users (tenant_id, manager_user_id);
                """);

            migrationBuilder.EnableTenantRowLevelSecurity("platform", "users");

            // 3. Create platform.refresh_tokens table
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS platform.refresh_tokens (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    user_id uuid NOT NULL,
                    family_id uuid NOT NULL,
                    token_hash text NOT NULL,
                    client_kind text NOT NULL,
                    device_label text NULL,
                    ip character varying(45) NULL,
                    user_agent text NULL,
                    issued_at timestamptz NOT NULL,
                    expires_at timestamptz NOT NULL,
                    rotated_at timestamptz NULL,
                    revoked_at timestamptz NULL,
                    revoke_reason text NULL,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    created_by uuid NULL,
                    CONSTRAINT ck_refresh_tokens_client_kind CHECK (client_kind IN ('web', 'mobile', 'api')),
                    CONSTRAINT ck_refresh_tokens_revoke_reason CHECK (revoke_reason IS NULL OR revoke_reason IN ('logout', 'reuse_detected', 'admin', 'password_changed', 'deactivated')),
                    CONSTRAINT ux_refresh_tokens_token_hash UNIQUE (token_hash),
                    CONSTRAINT fk_refresh_tokens_tenant_user FOREIGN KEY (tenant_id, user_id)
                        REFERENCES platform.users(tenant_id, id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ix_refresh_tokens_user_revoked ON platform.refresh_tokens (user_id, revoked_at);
                CREATE INDEX IF NOT EXISTS ix_refresh_tokens_family ON platform.refresh_tokens (family_id);
                CREATE INDEX IF NOT EXISTS ix_refresh_tokens_tenant_family ON platform.refresh_tokens (tenant_id, family_id);
                """);

            migrationBuilder.EnableTenantRowLevelSecurity("platform", "refresh_tokens");

            // 4. Create SECURITY DEFINER lookup function for login
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION platform.lookup_user_for_login(p_email text)
                RETURNS TABLE (
                    id uuid,
                    tenant_id uuid,
                    email text,
                    password_hash text,
                    status text,
                    failed_login_count int,
                    locked_until timestamptz,
                    failed_login_window_started_at timestamptz
                )
                LANGUAGE sql
                SECURITY DEFINER
                SET search_path = platform, pg_temp
                AS $$
                    SELECT u.id, u.tenant_id, u.email::text, u.password_hash, u.status,
                           u.failed_login_count, u.locked_until, u.failed_login_window_started_at
                    FROM platform.users u
                    WHERE u.email = lower(trim(p_email))::public.citext
                    LIMIT 1;
                $$;

                REVOKE ALL ON FUNCTION platform.lookup_user_for_login(text) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION platform.lookup_user_for_login(text) TO app;
                """);

            // 5. Create SECURITY DEFINER lookup and row-lock function for refresh tokens (minimized projection)
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION platform.locate_and_lock_refresh_token(p_token_hash text)
                RETURNS TABLE (
                    id uuid,
                    tenant_id uuid,
                    user_id uuid,
                    family_id uuid,
                    expires_at timestamptz,
                    rotated_at timestamptz,
                    revoked_at timestamptz
                )
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = platform, pg_temp
                AS $$
                DECLARE
                    v_rec platform.refresh_tokens%ROWTYPE;
                BEGIN
                    SELECT * INTO v_rec
                    FROM platform.refresh_tokens
                    WHERE token_hash = p_token_hash
                    FOR UPDATE;

                    IF FOUND THEN
                        RETURN QUERY SELECT
                            v_rec.id, v_rec.tenant_id, v_rec.user_id, v_rec.family_id,
                            v_rec.expires_at, v_rec.rotated_at, v_rec.revoked_at;
                    END IF;
                END;
                $$;

                REVOKE ALL ON FUNCTION platform.locate_and_lock_refresh_token(text) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION platform.locate_and_lock_refresh_token(text) TO app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS platform.locate_and_lock_refresh_token(text);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS platform.lookup_user_for_login(text);");
            migrationBuilder.DisableTenantRowLevelSecurity("platform", "refresh_tokens");
            migrationBuilder.Sql("DROP TABLE IF EXISTS platform.refresh_tokens;");
            migrationBuilder.DisableTenantRowLevelSecurity("platform", "users");
            migrationBuilder.Sql("DROP TABLE IF EXISTS platform.users;");
        }
    }
}
