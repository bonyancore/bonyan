using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Persistence;
using Construction.Procurement.Core.Domain.PurchaseRequests;
using Construction.Procurement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Construction.Procurement.Integration.Tests;

public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("INTEGRATION_TESTS_ENABLED"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Docker integration tests are disabled. Set INTEGRATION_TESTS_ENABLED=true to enable.";
        }
    }
}

public sealed class ProcurementPostgreSqlFixture : IAsyncLifetime
{
    public PostgreSqlContainer? Container { get; private set; }

    public string ConnectionString => Container?.GetConnectionString() ?? string.Empty;

    public async Task InitializeAsync()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("INTEGRATION_TESTS_ENABLED"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

#pragma warning disable CS0618
        Container = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("procurement_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
#pragma warning restore CS0618

        await Container.StartAsync();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE ROLE app LOGIN PASSWORD 'app_dev_pw' NOSUPERUSER NOBYPASSRLS;
            GRANT CONNECT ON DATABASE procurement_test TO app;
            CREATE SCHEMA procurement AUTHORIZATION postgres;
            GRANT USAGE ON SCHEMA procurement TO app;
            ALTER DEFAULT PRIVILEGES IN SCHEMA procurement
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app;
            ALTER DEFAULT PRIVILEGES IN SCHEMA procurement
                GRANT USAGE, SELECT ON SEQUENCES TO app;
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (Container is not null)
            await Container.DisposeAsync();
    }
}

public sealed class ProcurementRlsTests(ProcurementPostgreSqlFixture fixture)
    : IClassFixture<ProcurementPostgreSqlFixture>
{
    [DockerFact]
    public async Task Migration_EnablesRlsAndAppRoleCannotReadAnotherTenant()
    {
        var adminOptions = CreateOptions(fixture.ConnectionString).Options;
        await using (var migrationContext = new ProcurementDbContext(adminOptions, new FakeTenantContext()))
            await migrationContext.Database.MigrateAsync();

        await AssertRlsPoliciesExist();

        var tenantA = TenantId.New();
        var tenantB = TenantId.New();
        await using (var seed = new ProcurementDbContext(adminOptions, new FakeTenantContext()))
        {
            seed.PurchaseRequests.AddRange(CreateRequest(tenantA), CreateRequest(tenantB));
            await seed.SaveChangesAsync();
        }

        var appConnectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw",
        }.ConnectionString;
        var tenantAContext = new FakeTenantContext(tenantA);
        var appOptions = CreateOptions(appConnectionString)
            .AddInterceptors(new TenantSessionInterceptor(tenantAContext))
            .Options;

        await using var appContext = new ProcurementDbContext(appOptions, tenantAContext);
        var visibleRequests = await appContext.PurchaseRequests
            .IgnoreQueryFilters()
            .Include(request => request.Items)
            .AsNoTracking()
            .ToListAsync();

        Assert.Single(visibleRequests);
        Assert.Equal(tenantA, visibleRequests[0].TenantId);
        Assert.Single(visibleRequests[0].Items);
        Assert.All(visibleRequests[0].Items, item => Assert.Equal(tenantA, item.TenantId));

        await AssertCrossTenantInsertIsRejected(appConnectionString, tenantA, tenantB);
    }

    private static DbContextOptionsBuilder<ProcurementDbContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable("__ef_migrations_history", ProcurementDbContext.Schema))
            .UseSnakeCaseNamingConvention();

    private async Task AssertRlsPoliciesExist()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT tablename, policyname
            FROM pg_policies
            WHERE schemaname = 'procurement'
            ORDER BY tablename;
            """;

        var policies = new List<(string Table, string Policy)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            policies.Add((reader.GetString(0), reader.GetString(1)));

        Assert.Contains(("purchase_requests", "ti_procurement_purchase_requests"), policies);
        Assert.Contains(("purchase_request_items", "ti_procurement_purchase_request_items"), policies);
        Assert.Contains(("suppliers", "ti_procurement_suppliers"), policies);
        Assert.Contains(("requests_for_quotation", "ti_procurement_requests_for_quotation"), policies);
        Assert.Contains(("purchase_orders", "ti_procurement_purchase_orders"), policies);
        Assert.Contains(("purchase_order_lines", "ti_procurement_purchase_order_lines"), policies);
        Assert.Contains(("purchase_order_revisions", "ti_procurement_purchase_order_revisions"), policies);
        Assert.Contains(("purchase_order_revision_lines", "ti_procurement_purchase_order_revision_lines"), policies);
        Assert.Contains(("goods_receipts", "ti_procurement_goods_receipts"), policies);
        Assert.Contains(("goods_receipt_lines", "ti_procurement_goods_receipt_lines"), policies);
        Assert.Contains(("rfq_invitations", "ti_procurement_rfq_invitations"), policies);
        Assert.Contains(("supplier_quotes", "ti_procurement_supplier_quotes"), policies);
        Assert.Contains(("supplier_quote_lines", "ti_procurement_supplier_quote_lines"), policies);
        Assert.Contains(("project_budgets", "ti_procurement_project_budgets"), policies);
        Assert.Contains(("supplier_invoices", "ti_procurement_supplier_invoices"), policies);
        Assert.Contains(("supplier_invoice_lines", "ti_procurement_supplier_invoice_lines"), policies);
        Assert.Contains(("outbox_messages", "ti_procurement_outbox_messages"), policies);
    }

    private static async Task AssertCrossTenantInsertIsRejected(
        string connectionString,
        TenantId activeTenant,
        TenantId otherTenant)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var setTenant = connection.CreateCommand())
        {
            setTenant.CommandText = "SELECT set_config('app.tenant_id', @tenant_id, false)";
            setTenant.Parameters.AddWithValue("tenant_id", activeTenant.ToString());
            await setTenant.ExecuteScalarAsync();
        }

        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO procurement.purchase_requests
                (id, tenant_id, project_id, requested_by, status, created_at, updated_at)
            VALUES
                (@id, @tenant_id, @project_id, @requested_by, 'draft', now(), now());
            """;
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("tenant_id", otherTenant.Value);
        insert.Parameters.AddWithValue("project_id", Guid.NewGuid());
        insert.Parameters.AddWithValue("requested_by", Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());

        Assert.Equal("42501", exception.SqlState);
        Assert.Contains("row-level security policy", exception.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    private static PurchaseRequest CreateRequest(TenantId tenantId)
    {
        var request = PurchaseRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            tenantId,
            DateTimeOffset.UtcNow);
        request.AddItem(new PurchaseRequestItem("Portland cement", new Quantity(10m, "BAG")));
        return request;
    }

    private sealed class FakeTenantContext(TenantId? tenant = null) : ITenantContext
    {
        public bool HasTenant => tenant.HasValue;

        public TenantId Current => tenant ?? throw new InvalidOperationException("No tenant is set.");
    }
}
