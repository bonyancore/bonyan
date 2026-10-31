using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Construction.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Construction.Persistence.Tests.Integration;

public class PostgreSqlTenantIsolationFixture : IAsyncLifetime
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
            .WithDatabase("construction_test")
            .WithUsername("postgres")
            .WithPassword("postgres_dev")
            .Build();
#pragma warning restore CS0618

        await Container.StartAsync();

        // Provision production-identical 'app' role and test fixture table
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'app') THEN
                    CREATE ROLE app LOGIN PASSWORD 'app_dev_pw' NOSUPERUSER NOBYPASSRLS;
                END IF;
            END $$;

            GRANT CONNECT ON DATABASE construction_test TO app;
            GRANT USAGE ON SCHEMA public TO app;

            CREATE TABLE IF NOT EXISTS test_tenancy_records (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                name text NOT NULL
            );

            GRANT SELECT, INSERT, UPDATE, DELETE ON test_tenancy_records TO app;

            ALTER TABLE test_tenancy_records ENABLE ROW LEVEL SECURITY;
            ALTER TABLE test_tenancy_records FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS ti_public_test_tenancy_records ON test_tenancy_records;
            CREATE POLICY ti_public_test_tenancy_records ON test_tenancy_records
                FOR ALL
                TO app
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

public class PostgreSqlTenantIsolationTests(PostgreSqlTenantIsolationFixture fixture)
    : IClassFixture<PostgreSqlTenantIsolationFixture>
{
    private class FakeTenantContext : ITenantContext
    {
        public bool HasTenant { get; set; }
        public TenantId Current { get; set; }
    }

    private class RelationalDiscoveryTestDbContext(
        DbContextOptions<RelationalDiscoveryTestDbContext> options,
        ITenantContext tenantContext)
        : ModuleDbContext(options, ExpectedSchema, tenantContext)
    {
        public const string ExpectedSchema = "test_discovery_schema";
        public const string ExpectedTable = "test_scoped_records";

        public DbSet<DiscoveryTestEntity> TestEntities => Set<DiscoveryTestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<DiscoveryTestEntity>(b =>
            {
                b.ToTable(ExpectedTable, ExpectedSchema);
            });
        }

        public class DiscoveryTestEntity : ITenantScopedEntity
        {
            public Guid Id { get; set; } = Guid.NewGuid();
            public TenantId TenantId { get; set; }
        }
    }

    [Fact]
    public void RelationalDiscovery_WithTenantScopedEntity_DiscoversTableMappingUnderNpgsql()
    {
        var builder = new DbContextOptionsBuilder<RelationalDiscoveryTestDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy;Username=dummy;Password=dummy");

        using var ctx = new RelationalDiscoveryTestDbContext(builder.Options, new FakeTenantContext());

        var entityType = ctx.Model.FindEntityType(typeof(RelationalDiscoveryTestDbContext.DiscoveryTestEntity));
        Assert.NotNull(entityType);
        Assert.True(typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType));

        var mappings = entityType.GetTableMappings().ToList();
        Assert.Single(mappings);

        var table = mappings[0].Table;
        Assert.Equal(RelationalDiscoveryTestDbContext.ExpectedSchema, table.Schema);
        Assert.Equal(RelationalDiscoveryTestDbContext.ExpectedTable, table.Name);
    }

    [Fact]
    public void MigrationBuilderRlsExtensions_DeterministicPolicyName_EnforcesLimits()
    {
        var name1 = MigrationBuilderRlsExtensions.GetDeterministicPolicyName("public", "users");
        Assert.Equal("ti_public_users", name1);

        var longTable = new string('a', 60);
        var name2 = MigrationBuilderRlsExtensions.GetDeterministicPolicyName("long_schema_name", longTable);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(name2) <= 63);
        Assert.StartsWith("ti_", name2);
    }

    [DockerFact]
    public async Task AppRole_Attributes_CannotBypassRls_AndIsNotSuperuser()
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT rolsuper, rolbypassrls FROM pg_roles WHERE rolname = 'app';";
        await using var reader = await cmd.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        var isSuper = reader.GetBoolean(0);
        var bypassRls = reader.GetBoolean(1);

        Assert.False(isSuper);
        Assert.False(bypassRls);
    }

    [DockerFact]
    public async Task Catalog_VerifiesRowLevelSecurity_OnTenantScopedTables()
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();

        // 1. Check relrowsecurity and relforcerowsecurity on test_tenancy_records
        await using (var cmdClass = conn.CreateCommand())
        {
            cmdClass.CommandText = """
                SELECT c.relrowsecurity, c.relforcerowsecurity
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'public' AND c.relname = 'test_tenancy_records';
                """;
            await using var reader = await cmdClass.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetBoolean(0)); // relrowsecurity
            Assert.True(reader.GetBoolean(1)); // relforcerowsecurity
        }

        // 2. Check full policy definition in pg_policies
        await using (var cmdPolicy = conn.CreateCommand())
        {
            cmdPolicy.CommandText = """
                SELECT policyname, permissive, roles, cmd, qual, with_check
                FROM pg_policies
                WHERE schemaname = 'public' AND tablename = 'test_tenancy_records';
                """;
            await using var reader = await cmdPolicy.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            Assert.Equal("ti_public_test_tenancy_records", reader.GetString(0));
            Assert.Equal("PERMISSIVE", reader.GetString(1));

            var roles = (string[])reader.GetValue(2);
            Assert.Contains("app", roles);

            Assert.Equal("ALL", reader.GetString(3));

            var qual = reader.GetString(4);
            Assert.Contains("tenant_id", qual);
            Assert.Contains("app.tenant_id", qual);

            var withCheck = reader.GetString(5);
            Assert.Contains("tenant_id", withCheck);
            Assert.Contains("app.tenant_id", withCheck);
        }
    }

    [DockerFact]
    public async Task Functional_TenantA_SelectIsolation_CannotSeeTenantBRows()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        // Seed rows using superuser (admin) connection
        await using (var adminConn = new NpgsqlConnection(fixture.ConnectionString))
        {
            await adminConn.OpenAsync();
            await using var seedCmd = adminConn.CreateCommand();
            seedCmd.CommandText = """
                INSERT INTO test_tenancy_records (id, tenant_id, name)
                VALUES (@a, @tA, 'RowA'), (@b, @tB, 'RowB')
                ON CONFLICT (id) DO NOTHING;
                """;
            seedCmd.Parameters.AddWithValue("a", Guid.NewGuid());
            seedCmd.Parameters.AddWithValue("tA", tenantA.Value);
            seedCmd.Parameters.AddWithValue("b", Guid.NewGuid());
            seedCmd.Parameters.AddWithValue("tB", tenantB.Value);
            await seedCmd.ExecuteNonQueryAsync();
        }

        // Query under 'app' role via TenantSessionInterceptor on managed connection
        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        var tenantACtx = new FakeTenantContext { HasTenant = true, Current = tenantA };
        var interceptor = new TenantSessionInterceptor(tenantACtx);
        var options = new DbContextOptionsBuilder<RelationalDiscoveryTestDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        await using var ctx = new RelationalDiscoveryTestDbContext(options, tenantACtx);
        await ctx.Database.OpenConnectionAsync();

        var conn = ctx.Database.GetDbConnection();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, tenant_id, name FROM test_tenancy_records;";
            await using var reader = await cmd.ExecuteReaderAsync();

            var visibleRows = new List<(Guid Id, Guid TenantId, string Name)>();
            while (await reader.ReadAsync())
            {
                visibleRows.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2)));
            }

            Assert.Single(visibleRows);
            Assert.Equal(tenantA.Value, visibleRows[0].TenantId);
            Assert.Equal("RowA", visibleRows[0].Name);
        }

        await ctx.Database.CloseConnectionAsync();
    }

    [DockerFact]
    public async Task Functional_UnauthenticatedSession_ReturnsZeroRows()
    {
        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        var unauthCtx = new FakeTenantContext { HasTenant = false };
        var interceptor = new TenantSessionInterceptor(unauthCtx);
        var options = new DbContextOptionsBuilder<RelationalDiscoveryTestDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        await using var ctx = new RelationalDiscoveryTestDbContext(options, unauthCtx);
        await ctx.Database.OpenConnectionAsync();

        var conn = ctx.Database.GetDbConnection();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM test_tenancy_records;";
            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(0, count);
        }

        await ctx.Database.CloseConnectionAsync();
    }

    [DockerFact]
    public async Task Functional_UpdateIsolation_CannotModifyTenantBRow()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();
        var bRecordId = Guid.NewGuid();

        await using (var adminConn = new NpgsqlConnection(fixture.ConnectionString))
        {
            await adminConn.OpenAsync();
            await using var seedCmd = adminConn.CreateCommand();
            seedCmd.CommandText = "INSERT INTO test_tenancy_records (id, tenant_id, name) VALUES (@id, @tB, 'RowB_Original');";
            seedCmd.Parameters.AddWithValue("id", bRecordId);
            seedCmd.Parameters.AddWithValue("tB", tenantB.Value);
            await seedCmd.ExecuteNonQueryAsync();
        }

        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        var tenantACtx = new FakeTenantContext { HasTenant = true, Current = tenantA };
        var interceptor = new TenantSessionInterceptor(tenantACtx);
        var options = new DbContextOptionsBuilder<RelationalDiscoveryTestDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        await using var ctx = new RelationalDiscoveryTestDbContext(options, tenantACtx);
        await ctx.Database.OpenConnectionAsync();

        var conn = ctx.Database.GetDbConnection();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE test_tenancy_records SET name = 'Hacked' WHERE id = @id;";
            cmd.Parameters.Add(new NpgsqlParameter("id", bRecordId));
            var affected = await cmd.ExecuteNonQueryAsync();
            Assert.Equal(0, affected);
        }

        await ctx.Database.CloseConnectionAsync();

        // Verify in DB that row remains unchanged
        await using (var verifyConn = new NpgsqlConnection(fixture.ConnectionString))
        {
            await verifyConn.OpenAsync();
            await using var verifyCmd = verifyConn.CreateCommand();
            verifyCmd.CommandText = "SELECT name FROM test_tenancy_records WHERE id = @id;";
            verifyCmd.Parameters.AddWithValue("id", bRecordId);
            var name = (string?)await verifyCmd.ExecuteScalarAsync();
            Assert.Equal("RowB_Original", name);
        }
    }

    [DockerFact]
    public async Task Functional_DeleteIsolation_CannotDeleteTenantBRow()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();
        var bRecordId = Guid.NewGuid();

        await using (var adminConn = new NpgsqlConnection(fixture.ConnectionString))
        {
            await adminConn.OpenAsync();
            await using var seedCmd = adminConn.CreateCommand();
            seedCmd.CommandText = "INSERT INTO test_tenancy_records (id, tenant_id, name) VALUES (@id, @tB, 'RowB_Keep');";
            seedCmd.Parameters.AddWithValue("id", bRecordId);
            seedCmd.Parameters.AddWithValue("tB", tenantB.Value);
            await seedCmd.ExecuteNonQueryAsync();
        }

        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        var tenantACtx = new FakeTenantContext { HasTenant = true, Current = tenantA };
        var interceptor = new TenantSessionInterceptor(tenantACtx);
        var options = new DbContextOptionsBuilder<RelationalDiscoveryTestDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        await using var ctx = new RelationalDiscoveryTestDbContext(options, tenantACtx);
        await ctx.Database.OpenConnectionAsync();

        var conn = ctx.Database.GetDbConnection();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM test_tenancy_records WHERE id = @id;";
            cmd.Parameters.Add(new NpgsqlParameter("id", bRecordId));
            var affected = await cmd.ExecuteNonQueryAsync();
            Assert.Equal(0, affected);
        }

        await ctx.Database.CloseConnectionAsync();

        // Verify row still exists in DB
        await using (var verifyConn = new NpgsqlConnection(fixture.ConnectionString))
        {
            await verifyConn.OpenAsync();
            await using var verifyCmd = verifyConn.CreateCommand();
            verifyCmd.CommandText = "SELECT COUNT(*) FROM test_tenancy_records WHERE id = @id;";
            verifyCmd.Parameters.AddWithValue("id", bRecordId);
            var count = Convert.ToInt64(await verifyCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(1, count);
        }
    }

    [DockerFact]
    public async Task Functional_InsertViolation_ThrowsPostgresException42501_WithCheckViolation()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        // Assert role 'app' possesses explicit INSERT grant on the table
        await using (var adminConn = new NpgsqlConnection(fixture.ConnectionString))
        {
            await adminConn.OpenAsync();
            await using var grantCmd = adminConn.CreateCommand();
            grantCmd.CommandText = "SELECT has_table_privilege('app', 'public.test_tenancy_records', 'INSERT');";
            var hasInsert = (bool?)await grantCmd.ExecuteScalarAsync();
            Assert.True(hasInsert, "Role 'app' must possess table INSERT privilege to isolate RLS policy rejection.");
        }

        var tenantACtx = new FakeTenantContext { HasTenant = true, Current = tenantA };
        var interceptor = new TenantSessionInterceptor(tenantACtx);
        var options = new DbContextOptionsBuilder<RelationalDiscoveryTestDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        await using var ctx = new RelationalDiscoveryTestDbContext(options, tenantACtx);
        await ctx.Database.OpenConnectionAsync();

        var conn = ctx.Database.GetDbConnection();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO test_tenancy_records (id, tenant_id, name) VALUES (@id, @tB, 'Spoofed');";
            cmd.Parameters.Add(new NpgsqlParameter("id", Guid.NewGuid()));
            cmd.Parameters.Add(new NpgsqlParameter("tB", tenantB.Value));

            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());

            // Postgres RLS WITH CHECK violation emits ERRCODE_INSUFFICIENT_PRIVILEGE (42501)
            Assert.Equal("42501", ex.SqlState);
            Assert.Contains("violates row-level security policy", ex.MessageText, StringComparison.OrdinalIgnoreCase);
        }

        await ctx.Database.CloseConnectionAsync();
    }

    [DockerFact]
    public async Task MigrationBuilderRlsExtensions_Enable_WhenTableDoesNotExist_ThrowsDescriptiveException()
    {
        var migrationBuilder = new MigrationBuilder("Npgsql");
        migrationBuilder.EnableTenantRowLevelSecurity("public", "non_existent_table");

        var sql = migrationBuilder.Operations.OfType<SqlOperation>().Single().Sql;

        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Contains("RLS prerequisite failed: Table public.non_existent_table does not exist", ex.MessageText);
    }

    [DockerFact]
    public async Task MigrationBuilderRlsExtensions_Enable_WhenTenantIdColumnMissing_ThrowsDescriptiveException()
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();

        await using (var setupCmd = conn.CreateCommand())
        {
            setupCmd.CommandText = "CREATE TABLE IF NOT EXISTS test_missing_tenant (id uuid PRIMARY KEY, name text);";
            await setupCmd.ExecuteNonQueryAsync();
        }

        var migrationBuilder = new MigrationBuilder("Npgsql");
        migrationBuilder.EnableTenantRowLevelSecurity("public", "test_missing_tenant");

        var sql = migrationBuilder.Operations.OfType<SqlOperation>().Single().Sql;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Contains("does not contain a tenant_id column", ex.MessageText);
    }

    [DockerFact]
    public async Task MigrationBuilderRlsExtensions_Enable_WhenTenantIdNotUuid_ThrowsDescriptiveException()
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();

        await using (var setupCmd = conn.CreateCommand())
        {
            setupCmd.CommandText = "CREATE TABLE IF NOT EXISTS test_wrong_tenant_type (id uuid PRIMARY KEY, tenant_id text, name text);";
            await setupCmd.ExecuteNonQueryAsync();
        }

        var migrationBuilder = new MigrationBuilder("Npgsql");
        migrationBuilder.EnableTenantRowLevelSecurity("public", "test_wrong_tenant_type");

        var sql = migrationBuilder.Operations.OfType<SqlOperation>().Single().Sql;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Contains("must be type uuid", ex.MessageText);
    }

    [DockerFact]
    public async Task MigrationBuilderRlsExtensions_Enable_WhenPolicyAlreadyExists_ThrowsDescriptiveException()
    {
        var migrationBuilder = new MigrationBuilder("Npgsql");
        // public.test_tenancy_records was already provisioned with ti_public_test_tenancy_records in fixture setup
        migrationBuilder.EnableTenantRowLevelSecurity("public", "test_tenancy_records");

        var sql = migrationBuilder.Operations.OfType<SqlOperation>().Single().Sql;

        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Contains("already exists. To modify an existing policy", ex.MessageText);
    }

    [DockerFact]
    public async Task MigrationBuilderRlsExtensions_Disable_WhenTableDoesNotExist_SucceedsIdempotently()
    {
        var migrationBuilder = new MigrationBuilder("Npgsql");
        migrationBuilder.DisableTenantRowLevelSecurity("public", "non_existent_table_for_disable");

        var sql = migrationBuilder.Operations.OfType<SqlOperation>().Single().Sql;

        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        // Idempotent disable on non-existent table must complete with 0 errors
        var affected = await cmd.ExecuteNonQueryAsync();
        Assert.True(affected >= -1);
    }
}
