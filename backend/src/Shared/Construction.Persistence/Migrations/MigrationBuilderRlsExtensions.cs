using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Construction.Persistence.Migrations;

public static class MigrationBuilderRlsExtensions
{
    public static void EnableTenantRowLevelSecurity(
        this MigrationBuilder migrationBuilder,
        string schema,
        string table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        ValidatePostgreSqlIdentifier(schema);
        ValidatePostgreSqlIdentifier(table);

        var policyName = GetDeterministicPolicyName(schema, table);

        migrationBuilder.Sql($"""
            DO $$
            DECLARE
                target_table_id oid;
                tenant_col_type text;
                role_is_super boolean;
                role_can_bypass boolean;
                existing_cmd text;
            BEGIN
                -- 1. Validate schema and table existence
                SELECT c.oid INTO target_table_id
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = '{schema}'
                  AND c.relname = '{table}'
                  AND c.relkind IN ('r', 'p');

                IF target_table_id IS NULL THEN
                    RAISE EXCEPTION 'RLS prerequisite failed: Table %.% does not exist.', '{schema}', '{table}';
                END IF;

                -- 2. Validate tenant_id column exists and is of type uuid
                SELECT t.typname INTO tenant_col_type
                FROM pg_attribute a
                JOIN pg_type t ON t.oid = a.atttypid
                WHERE a.attrelid = target_table_id
                  AND a.attname = 'tenant_id'
                  AND NOT a.attisdropped;

                IF tenant_col_type IS NULL THEN
                    RAISE EXCEPTION 'RLS prerequisite failed: Table %.% does not contain a tenant_id column.', '{schema}', '{table}';
                ELSIF tenant_col_type <> 'uuid' THEN
                    RAISE EXCEPTION 'RLS prerequisite failed: Column tenant_id in %.% must be type uuid, but found %.', '{schema}', '{table}', tenant_col_type;
                END IF;

                -- 3. Validate app role existence and security attributes (rolsuper=false, rolbypassrls=false)
                SELECT rolsuper, rolbypassrls INTO role_is_super, role_can_bypass
                FROM pg_roles
                WHERE rolname = 'app';

                IF role_is_super IS NULL THEN
                    RAISE EXCEPTION 'RLS prerequisite failed: Application database role ''app'' does not exist. Ensure database provisioning has run.';
                ELSIF role_is_super = TRUE THEN
                    RAISE EXCEPTION 'RLS prerequisite failed: Role ''app'' has SUPERUSER privileges, which bypasses Row Level Security.';
                ELSIF role_can_bypass = TRUE THEN
                    RAISE EXCEPTION 'RLS prerequisite failed: Role ''app'' has BYPASSRLS privileges, which bypasses Row Level Security.';
                END IF;

                -- 4. Fail-closed on duplicate policy
                SELECT cmd INTO existing_cmd
                FROM pg_policies
                WHERE schemaname = '{schema}' AND tablename = '{table}' AND policyname = '{policyName}';

                IF existing_cmd IS NOT NULL THEN
                    RAISE EXCEPTION 'RLS Policy % on %.% already exists. To modify an existing policy, provide an explicit replacement migration.',
                        '{policyName}', '{schema}', '{table}';
                END IF;

                -- 5. Enable RLS, Force RLS, and Create Policy
                EXECUTE 'ALTER TABLE "{schema}"."{table}" ENABLE ROW LEVEL SECURITY';
                EXECUTE 'ALTER TABLE "{schema}"."{table}" FORCE ROW LEVEL SECURITY';
                EXECUTE 'CREATE POLICY "{policyName}" ON "{schema}"."{table}" ' ||
                        'FOR ALL TO app ' ||
                        'USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')' || '::uuid) ' ||
                        'WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')' || '::uuid)';
            END $$;
            """);
    }

    public static void DisableTenantRowLevelSecurity(
        this MigrationBuilder migrationBuilder,
        string schema,
        string table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        ValidatePostgreSqlIdentifier(schema);
        ValidatePostgreSqlIdentifier(table);

        var policyName = GetDeterministicPolicyName(schema, table);

        migrationBuilder.Sql($"""
            DO $$
            DECLARE
                target_table_id oid;
            BEGIN
                SELECT c.oid INTO target_table_id
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = '{schema}'
                  AND c.relname = '{table}'
                  AND c.relkind IN ('r', 'p');

                IF target_table_id IS NOT NULL THEN
                    EXECUTE 'DROP POLICY IF EXISTS "{policyName}" ON "{schema}"."{table}"';
                    EXECUTE 'ALTER TABLE "{schema}"."{table}" NO FORCE ROW LEVEL SECURITY';
                    EXECUTE 'ALTER TABLE "{schema}"."{table}" DISABLE ROW LEVEL SECURITY';
                END IF;
            END $$;
            """);
    }

    public static string GetDeterministicPolicyName(string schema, string table)
    {
        var raw = $"ti_{schema}_{table}";
        if (Encoding.UTF8.GetByteCount(raw) <= 63)
        {
            return raw;
        }

        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw)))[..8].ToLowerInvariant();
        return $"ti_{raw[..48]}_{hash}";
    }

    private static void ValidatePostgreSqlIdentifier(string identifier)
    {
        var bytes = Encoding.UTF8.GetByteCount(identifier);
        if (bytes == 0 || bytes > 63)
        {
            throw new ArgumentException($"SQL identifier '{identifier}' exceeds PostgreSQL maximum identifier length of 63 bytes.");
        }

        if (!Regex.IsMatch(identifier, "^[a-zA-Z0-9_]+$"))
        {
            throw new ArgumentException($"SQL identifier '{identifier}' contains invalid characters. Must match ^[a-zA-Z0-9_]+$.");
        }
    }
}

