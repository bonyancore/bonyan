using Construction.Kernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Construction.Persistence.Tests.Integration;

/// <summary>
/// Custom fact attribute that skips the test unless LIVE_PG_ENABLED=true is set.
/// Counterpart to <see cref="DockerFactAttribute"/> for the live localhost:5432 database.
/// </summary>
public sealed class LiveDatabaseFactAttribute : FactAttribute
{
    public LiveDatabaseFactAttribute()
    {
        var enabled = Environment.GetEnvironmentVariable("LIVE_PG_ENABLED");
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Live-database tests are disabled. Set LIVE_PG_ENABLED=true to enable.";
        }
    }
}

/// <summary>
/// Fixture that connects directly to the live PostgreSQL instance on localhost:5432,
/// provisions a test-only table with RLS enabled+forced under the 'app' role policy,
/// and tears it down in DisposeAsync.
/// </summary>
public sealed class LivePostgreSqlFixture : IAsyncLifetime
{
    // Admin / superuser connection — bypasses RLS for seeding and verification.
    public const string AdminConnectionString =
        "Host=localhost;Port=5432;Database=construction;Username=postgres;Password=postgres_dev";

    // Restricted app-role connection — subject to RLS.
    public const string AppConnectionString =
        "Host=localhost;Port=5432;Database=construction;Username=app;Password=app_dev_pw";

    // Dispatcher-role connection — NOSUPERUSER, NOBYPASSRLS, special outbox policy.
    public const string DispatcherConnectionString =
        "Host=localhost;Port=5432;Database=construction;Username=cc_outbox_dispatcher;Password=dispatcher_dev_pw";

    private const string TestTable = "test_live_tenancy_records";

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE IF NOT EXISTS public.{TestTable} (
                id        uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                name      text NOT NULL
            );

            GRANT SELECT, INSERT, UPDATE, DELETE ON public.{TestTable} TO app;

            ALTER TABLE public.{TestTable} ENABLE ROW LEVEL SECURITY;
            ALTER TABLE public.{TestTable} FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS ti_public_{TestTable} ON public.{TestTable};
            CREATE POLICY ti_public_{TestTable} ON public.{TestTable}
                FOR ALL
                TO app
                USING  (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP TABLE IF EXISTS public.{TestTable};";
        await cmd.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Live-database RLS proof tests for PLT-104.
/// All tests run against localhost:5432 / database=construction with the provisioned roles.
/// </summary>
public sealed class LivePostgreSqlTenantIsolationTests : IClassFixture<LivePostgreSqlFixture>
{
    // fixture is injected by xUnit and held here to satisfy the IClassFixture<T> contract.
#pragma warning disable IDE0052
    private readonly LivePostgreSqlFixture _fixture;
#pragma warning restore IDE0052

    public LivePostgreSqlTenantIsolationTests(LivePostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private const string TestTable = "test_live_tenancy_records";

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Open an app-role connection with app.tenant_id already set.</summary>
    private static async Task<NpgsqlConnection> OpenAppConnectionAsync(string tenantId)
    {
        var conn = new NpgsqlConnection(LivePostgreSqlFixture.AppConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.tenant_id', @t, false)";
        cmd.Parameters.AddWithValue("t", tenantId);
        await cmd.ExecuteNonQueryAsync();
        return conn;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // a. Role attribute checks
    // ─────────────────────────────────────────────────────────────────────────

    [LiveDatabaseFact]
    public async Task AppRole_CannotBypassRls_AndIsNotSuperuser()
    {
        await using var conn = new NpgsqlConnection(LivePostgreSqlFixture.AdminConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT rolsuper, rolbypassrls FROM pg_roles WHERE rolname = 'app';";
        await using var reader = await cmd.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync(), "Role 'app' not found in pg_roles.");
        Assert.False(reader.GetBoolean(0), "app must NOT be a superuser.");
        Assert.False(reader.GetBoolean(1), "app must NOT bypass RLS.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // b. Unauthenticated session (sentinel UUID) → 0 rows
    // ─────────────────────────────────────────────────────────────────────────

    [LiveDatabaseFact]
    public async Task Functional_UnauthenticatedSession_ReturnsZeroRows()
    {
        // Seed a real tenant row via admin so the table is not empty.
        var realTenant = Guid.NewGuid();
        await using (var adminConn = new NpgsqlConnection(LivePostgreSqlFixture.AdminConnectionString))
        {
            await adminConn.OpenAsync();
            await using var seedCmd = adminConn.CreateCommand();
            seedCmd.CommandText =
                $"INSERT INTO public.{TestTable} (id, tenant_id, name) VALUES (@id, @t, 'SeedRow') ON CONFLICT DO NOTHING;";
            seedCmd.Parameters.AddWithValue("id", Guid.NewGuid());
            seedCmd.Parameters.AddWithValue("t", realTenant);
            await seedCmd.ExecuteNonQueryAsync();
        }

        // TenantSessionInterceptor sentinel = "00000000-0000-0000-0000-000000000000"
        // The RLS NULLIF(...,'')::uuid casts it to a valid UUID that matches no tenant → 0 rows.
        await using var conn = await OpenAppConnectionAsync("00000000-0000-0000-0000-000000000000");
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM public.{TestTable};";
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(0, count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // c. Tenant A select isolation
    // ─────────────────────────────────────────────────────────────────────────

    [LiveDatabaseFact]
    public async Task Functional_TenantA_SelectIsolation_CannotSeeTenantBRows()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Seed both tenants as admin.
        await using (var adminConn = new NpgsqlConnection(LivePostgreSqlFixture.AdminConnectionString))
        {
            await adminConn.OpenAsync();
            await using var seedCmd = adminConn.CreateCommand();
            seedCmd.CommandText = $"""
                INSERT INTO public.{TestTable} (id, tenant_id, name)
                VALUES (@a, @tA, 'RowA'), (@b, @tB, 'RowB')
                ON CONFLICT DO NOTHING;
                """;
            seedCmd.Parameters.AddWithValue("a", Guid.NewGuid());
            seedCmd.Parameters.AddWithValue("tA", tenantA);
            seedCmd.Parameters.AddWithValue("b", Guid.NewGuid());
            seedCmd.Parameters.AddWithValue("tB", tenantB);
            await seedCmd.ExecuteNonQueryAsync();
        }

        // Query as Tenant A.
        await using var conn = await OpenAppConnectionAsync(tenantA.ToString());
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT tenant_id, name FROM public.{TestTable};";
        await using var reader = await cmd.ExecuteReaderAsync();

        var rows = new List<(Guid TenantId, string Name)>();
        while (await reader.ReadAsync())
            rows.Add((reader.GetGuid(0), reader.GetString(1)));

        Assert.All(rows, r => Assert.Equal(tenantA, r.TenantId));
        Assert.Contains(rows, r => r.Name == "RowA");
        Assert.DoesNotContain(rows, r => r.Name == "RowB");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // d. Update isolation
    // ─────────────────────────────────────────────────────────────────────────

    [LiveDatabaseFact]
    public async Task Functional_UpdateIsolation_CannotModifyTenantBRow()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var bRecordId = Guid.NewGuid();

        await using (var adminConn = new NpgsqlConnection(LivePostgreSqlFixture.AdminConnectionString))
        {
            await adminConn.OpenAsync();
            await using var seedCmd = adminConn.CreateCommand();
            seedCmd.CommandText =
                $"INSERT INTO public.{TestTable} (id, tenant_id, name) VALUES (@id, @t, 'Original') ON CONFLICT DO NOTHING;";
            seedCmd.Parameters.AddWithValue("id", bRecordId);
            seedCmd.Parameters.AddWithValue("t", tenantB);
            await seedCmd.ExecuteNonQueryAsync();
        }

        // Try to update Tenant B's row while authenticated as Tenant A.
        await using (var appConn = await OpenAppConnectionAsync(tenantA.ToString()))
        {
            await using var cmd = appConn.CreateCommand();
            cmd.CommandText = $"UPDATE public.{TestTable} SET name = 'Hacked' WHERE id = @id;";
            cmd.Parameters.AddWithValue("id", bRecordId);
            var affected = await cmd.ExecuteNonQueryAsync();
            Assert.Equal(0, affected);
        }

        // Verify the row is unchanged via admin.
        await using (var adminConn = new NpgsqlConnection(LivePostgreSqlFixture.AdminConnectionString))
        {
            await adminConn.OpenAsync();
            await using var vCmd = adminConn.CreateCommand();
            vCmd.CommandText = $"SELECT name FROM public.{TestTable} WHERE id = @id;";
            vCmd.Parameters.AddWithValue("id", bRecordId);
            var name = (string?)await vCmd.ExecuteScalarAsync();
            Assert.Equal("Original", name);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // e. Delete isolation
    // ─────────────────────────────────────────────────────────────────────────

    [LiveDatabaseFact]
    public async Task Functional_DeleteIsolation_CannotDeleteTenantBRow()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var bRecordId = Guid.NewGuid();

        await using (var adminConn = new NpgsqlConnection(LivePostgreSqlFixture.AdminConnectionString))
        {
            await adminConn.OpenAsync();
            await using var seedCmd = adminConn.CreateCommand();
            seedCmd.CommandText =
                $"INSERT INTO public.{TestTable} (id, tenant_id, name) VALUES (@id, @t, 'KeepMe') ON CONFLICT DO NOTHING;";
            seedCmd.Parameters.AddWithValue("id", bRecordId);
            seedCmd.Parameters.AddWithValue("t", tenantB);
            await seedCmd.ExecuteNonQueryAsync();
        }

        await using (var appConn = await OpenAppConnectionAsync(tenantA.ToString()))
        {
            await using var cmd = appConn.CreateCommand();
            cmd.CommandText = $"DELETE FROM public.{TestTable} WHERE id = @id;";
            cmd.Parameters.AddWithValue("id", bRecordId);
            var affected = await cmd.ExecuteNonQueryAsync();
            Assert.Equal(0, affected);
        }

        await using (var adminConn = new NpgsqlConnection(LivePostgreSqlFixture.AdminConnectionString))
        {
            await adminConn.OpenAsync();
            await using var vCmd = adminConn.CreateCommand();
            vCmd.CommandText = $"SELECT COUNT(*) FROM public.{TestTable} WHERE id = @id;";
            vCmd.Parameters.AddWithValue("id", bRecordId);
            var count = Convert.ToInt64(await vCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(1, count);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // f. INSERT WITH CHECK violation → 42501
    // ─────────────────────────────────────────────────────────────────────────

    [LiveDatabaseFact]
    public async Task Functional_InsertViolation_ThrowsPostgresException42501()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Confirm app has INSERT privilege (prerequisite for the RLS policy to be the rejection).
        await using (var adminConn = new NpgsqlConnection(LivePostgreSqlFixture.AdminConnectionString))
        {
            await adminConn.OpenAsync();
            await using var grantCmd = adminConn.CreateCommand();
            grantCmd.CommandText = $"SELECT has_table_privilege('app', 'public.{TestTable}', 'INSERT');";
            var hasInsert = (bool?)await grantCmd.ExecuteScalarAsync();
            Assert.True(hasInsert, "Role 'app' must have INSERT privilege so RLS WITH CHECK is what rejects the insert.");
        }

        await using var appConn = await OpenAppConnectionAsync(tenantA.ToString());
        await using var cmd = appConn.CreateCommand();

        // Try to insert a row for Tenant B while session says Tenant A → WITH CHECK fails.
        cmd.CommandText = $"INSERT INTO public.{TestTable} (id, tenant_id, name) VALUES (@id, @tB, 'Spoofed');";
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("tB", tenantB);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal("42501", ex.SqlState);
        Assert.Contains("violates row-level security policy", ex.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // g. Dispatcher role can read all tenants' outbox rows (cross-tenant claim)
    // ─────────────────────────────────────────────────────────────────────────

    [LiveDatabaseFact]
    public async Task DispatcherRole_CanClaimAllTenants_OutboxRows()
    {
        var tenantId = Guid.NewGuid();
        var rowId = Guid.NewGuid();

        // Insert a row into platform.outbox as postgres (admin).
        await using (var adminConn = new NpgsqlConnection(LivePostgreSqlFixture.AdminConnectionString))
        {
            await adminConn.OpenAsync();
            await using var insertCmd = adminConn.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO platform.outbox
                    (id, tenant_id, aggregate_type, aggregate_id, aggregate_version, type, payload, occurred_at)
                VALUES
                    (@id, @tid, 'TestAggregate', @aid, 1, 'TestEvent', '{}', now())
                ON CONFLICT DO NOTHING;
                """;
            insertCmd.Parameters.AddWithValue("id", rowId);
            insertCmd.Parameters.AddWithValue("tid", tenantId);
            insertCmd.Parameters.AddWithValue("aid", Guid.NewGuid());
            await insertCmd.ExecuteNonQueryAsync();
        }

        // Connect as dispatcher — no need to set app.tenant_id; outbox_dispatcher_claim policy USING(true).
        await using var dispConn = new NpgsqlConnection(LivePostgreSqlFixture.DispatcherConnectionString);
        await dispConn.OpenAsync();

        await using var selCmd = dispConn.CreateCommand();
        selCmd.CommandText = "SELECT COUNT(*) FROM platform.outbox WHERE id = @id;";
        selCmd.Parameters.AddWithValue("id", rowId);
        var count = Convert.ToInt64(await selCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(1, count);

        // Cleanup the test row.
        await using (var adminConn = new NpgsqlConnection(LivePostgreSqlFixture.AdminConnectionString))
        {
            await adminConn.OpenAsync();
            await using var del = adminConn.CreateCommand();
            del.CommandText = "DELETE FROM platform.outbox WHERE id = @id;";
            del.Parameters.AddWithValue("id", rowId);
            await del.ExecuteNonQueryAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // h. Dispatcher role cannot read platform.tenants (no GRANT → 42501 / 42P01)
    // ─────────────────────────────────────────────────────────────────────────

    [LiveDatabaseFact]
    public async Task DispatcherRole_CannotReadTenantBusinessTables()
    {
        await using var dispConn = new NpgsqlConnection(LivePostgreSqlFixture.DispatcherConnectionString);
        await dispConn.OpenAsync();

        await using var cmd = dispConn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM platform.tenants;";

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteScalarAsync());

        // 42501 = insufficient_privilege  |  42P01 = undefined_table (no USAGE on schema either)
        Assert.True(
            ex.SqlState is "42501" or "42P01",
            $"Expected 42501 or 42P01 but got '{ex.SqlState}': {ex.MessageText}");
    }
}
