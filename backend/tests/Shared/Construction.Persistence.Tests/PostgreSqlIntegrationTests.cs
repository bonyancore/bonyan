using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Construction.Persistence.Tests.Integration;

public class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        var enabled = Environment.GetEnvironmentVariable("INTEGRATION_TESTS_ENABLED");
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Docker integration tests are disabled. Set INTEGRATION_TESTS_ENABLED=true to enable.";
        }
    }
}

public class PostgreSqlIntegrationTestsFixture : IAsyncLifetime
{
    public PostgreSqlContainer? Container { get; private set; }
    public string ConnectionString => Container?.GetConnectionString() ?? string.Empty;

    public async Task InitializeAsync()
    {
        var enabled = Environment.GetEnvironmentVariable("INTEGRATION_TESTS_ENABLED");
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

#pragma warning disable CS0618
        Container = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("test_db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
#pragma warning restore CS0618

        await Container.StartAsync();

        // Provision test application role and table with RLS
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'test_app') THEN
                    CREATE ROLE test_app LOGIN PASSWORD 'test_pw' NOSUPERUSER NOBYPASSRLS;
                END IF;
            END $$;

            GRANT CONNECT ON DATABASE test_db TO test_app;
            GRANT USAGE ON SCHEMA public TO test_app;

            CREATE TABLE IF NOT EXISTS test_tenancy_records (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                name text NOT NULL
            );

            GRANT SELECT, INSERT, UPDATE, DELETE ON test_tenancy_records TO test_app;

            ALTER TABLE test_tenancy_records ENABLE ROW LEVEL SECURITY;
            ALTER TABLE test_tenancy_records FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS tenant_isolation_policy ON test_tenancy_records;
            CREATE POLICY tenant_isolation_policy ON test_tenancy_records
                FOR ALL
                TO test_app
                USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (Container is not null)
        {
            await Container.DisposeAsync();
        }
    }
}

public class PostgreSqlTenantQueryFilterIntegrationTests(PostgreSqlIntegrationTestsFixture fixture)
    : IClassFixture<PostgreSqlIntegrationTestsFixture>
{
    private class TestEntity : ITenantScopedEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public TenantId TenantId { get; set; }
    }

    private class IntegrationDbContext(DbContextOptions<IntegrationDbContext> options, ITenantContext tenantContext)
        : ModuleDbContext(options, "public", tenantContext)
    {
        public DbSet<TestEntity> TestEntities => Set<TestEntity>();
    }

    private class FakeTenantContext : ITenantContext
    {
        public bool HasTenant { get; set; }
        public TenantId Current { get; set; }
    }

    [DockerFact]
    public async Task Query_UnderTenantA_ReturnsOnlyTenantARecords()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        var adminBuilder = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseNpgsql(fixture.ConnectionString);

        var adminTenantCtx = new FakeTenantContext();
        await using (var adminCtx = new IntegrationDbContext(adminBuilder.Options, adminTenantCtx))
        {
            // FIX 1: EnsureCreatedAsync() returns false when the DB already exists (Testcontainers).
            // Use CreateTablesAsync() instead — it only creates the tables defined in this DbContext
            // (TestEntity → "TestEntities") without touching any other fixture tables.
            var creator = adminCtx.Database.GetService<IRelationalDatabaseCreator>();
            await creator.CreateTablesAsync();

            adminCtx.TestEntities.AddRange(
                new TestEntity { Name = "RowA", TenantId = tenantA },
                new TestEntity { Name = "RowB", TenantId = tenantB });
            await adminCtx.SaveChangesAsync();
        }

        // Query under Tenant A
        var tenantACtx = new FakeTenantContext { HasTenant = true, Current = tenantA };
        await using (var queryCtx = new IntegrationDbContext(adminBuilder.Options, tenantACtx))
        {
            var rows = await queryCtx.TestEntities.ToListAsync();
            Assert.Single(rows);
            Assert.Equal("RowA", rows[0].Name);
            Assert.Equal(tenantA, rows[0].TenantId);
        }
    }

    [DockerFact]
    public async Task ConnectionPooling_ReusedConnection_SamePid_ResetsTenantSessionParameter()
    {
        // Dedicated connection string with MaxPoolSize=1 to guarantee physical connection reuse.
        // Uses the non-superuser test_app role so that RLS policies are enforced.
        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "test_app",
            Password = "test_pw",
            MaxPoolSize = 1,
            MinPoolSize = 1,
            Pooling = true
        };

        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        // ── Seed both tenants' rows using the admin (postgres) superuser connection ──
        // The admin connection bypasses RLS so rows for both tenants can be inserted.
        await using (var adminConn = new NpgsqlConnection(fixture.ConnectionString))
        {
            await adminConn.OpenAsync();
            await using var seedCmd = adminConn.CreateCommand();
            seedCmd.CommandText = """
                INSERT INTO test_tenancy_records (id, tenant_id, name)
                VALUES (@a_id, @tenant_a, 'RowA'), (@b_id, @tenant_b, 'RowB')
                ON CONFLICT (id) DO NOTHING;
                """;
            seedCmd.Parameters.AddWithValue("a_id", Guid.NewGuid());
            seedCmd.Parameters.AddWithValue("tenant_a", tenantA.Value);
            seedCmd.Parameters.AddWithValue("b_id", Guid.NewGuid());
            seedCmd.Parameters.AddWithValue("tenant_b", tenantB.Value);
            await seedCmd.ExecuteNonQueryAsync();
        }

        int pid1;
        int pid2;

        // ── Context 1: Tenant A ──────────────────────────────────────────────────────
        var tenantACtx = new FakeTenantContext { HasTenant = true, Current = tenantA };
        var interceptorA = new TenantSessionInterceptor(tenantACtx);
        var optionsA = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .AddInterceptors(interceptorA)
            .Options;

        await using (var ctx1 = new IntegrationDbContext(optionsA, tenantACtx))
        {
            // FIX 2a: Use OpenConnectionAsync() so EF Core's interception pipeline fires
            // and TenantSessionInterceptor.ConnectionOpenedAsync executes set_config().
            await ctx1.Database.OpenConnectionAsync();

            // Get the raw connection after EF Core has opened it (interceptor already ran).
            var conn1 = ctx1.Database.GetDbConnection();

            // FIX 2b: Use current_setting('app.tenant_id', true) (missing_ok=true) to avoid
            // error 42704 when the parameter was never set on this connection.
            // Dispose cmd + reader before CloseConnectionAsync so the connection is free.
            await using (var cmd = conn1.CreateCommand())
            {
                cmd.CommandText = "SELECT pg_backend_pid(), current_setting('app.tenant_id', true)";
                await using var reader = await cmd.ExecuteReaderAsync();
                await reader.ReadAsync();
                pid1 = reader.GetInt32(0);
                var activeSetting = reader.GetString(1);
                Assert.Equal(tenantA.ToString(), activeSetting);
            }

            // Close EF Core's tracked connection so the physical connection returns to the pool.
            await ctx1.Database.CloseConnectionAsync();
        }

        // ── Context 2: Tenant B on the reused physical connection ────────────────────
        var tenantBCtx = new FakeTenantContext { HasTenant = true, Current = tenantB };
        var interceptorB = new TenantSessionInterceptor(tenantBCtx);
        var optionsB = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .AddInterceptors(interceptorB)
            .Options;

        await using (var ctx2 = new IntegrationDbContext(optionsB, tenantBCtx))
        {
            // FIX 2a (repeated): Open via EF Core so the interceptor fires for Tenant B.
            await ctx2.Database.OpenConnectionAsync();

            var conn2 = ctx2.Database.GetDbConnection();

            // ── PID + session parameter verification ──────────────────────────────
            await using (var pidCmd = conn2.CreateCommand())
            {
                pidCmd.CommandText = "SELECT pg_backend_pid(), current_setting('app.tenant_id', true)";
                await using var pidReader = await pidCmd.ExecuteReaderAsync();
                await pidReader.ReadAsync();

                pid2 = pidReader.GetInt32(0);
                var activeSetting = pidReader.GetString(1);

                // Same backend PID → physical connection was reused from the pool.
                Assert.Equal(pid1, pid2);

                // Interceptor must have reset the session parameter for Tenant B.
                Assert.Equal(tenantB.ToString(), activeSetting);
            }

            // ── RLS isolation verification ────────────────────────────────────────
            // The test_app role has NOBYPASSRLS, so the RLS policy applies.
            // With app.tenant_id = tenantB, only Tenant B's row should be visible.
            var visibleRows = new List<(Guid Id, Guid TenantId, string Name)>();
            await using (var rlsCmd = conn2.CreateCommand())
            {
                rlsCmd.CommandText = "SELECT id, tenant_id, name FROM test_tenancy_records";
                await using var rlsReader = await rlsCmd.ExecuteReaderAsync();
                while (await rlsReader.ReadAsync())
                {
                    visibleRows.Add((rlsReader.GetGuid(0), rlsReader.GetGuid(1), rlsReader.GetString(2)));
                }
            }

            // Exactly one row must be visible — Tenant B's row.
            Assert.Single(visibleRows);
            Assert.Equal(tenantB.Value, visibleRows[0].TenantId);
            Assert.Equal("RowB", visibleRows[0].Name);

            await ctx2.Database.CloseConnectionAsync();
        }
    }
}
