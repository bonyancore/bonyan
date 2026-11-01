using Construction.Docs.Infrastructure.Persistence;
using Construction.Kernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Construction.Docs.Integration.Tests;

public sealed class DocsDockerFactAttribute : FactAttribute
{
    public DocsDockerFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("INTEGRATION_TESTS_ENABLED"), "true", StringComparison.OrdinalIgnoreCase))
            Skip = "Docker integration tests are disabled. Set INTEGRATION_TESTS_ENABLED=true to enable.";
    }
}

public sealed class DocsPostgreSqlFixture : IAsyncLifetime
{
    public PostgreSqlContainer? Container { get; private set; }

    public string ConnectionString => Container?.GetConnectionString() ?? string.Empty;

    public async Task InitializeAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("INTEGRATION_TESTS_ENABLED"), "true", StringComparison.OrdinalIgnoreCase))
            return;
#pragma warning disable CS0618
        Container = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("docs_test")
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
            GRANT CONNECT ON DATABASE docs_test TO app;
            CREATE SCHEMA docs AUTHORIZATION postgres;
            GRANT USAGE ON SCHEMA docs TO app;
            ALTER DEFAULT PRIVILEGES IN SCHEMA docs GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app;
            ALTER DEFAULT PRIVILEGES IN SCHEMA docs GRANT USAGE, SELECT ON SEQUENCES TO app;
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (Container is not null)
            await Container.DisposeAsync();
    }
}

public sealed class DocsRlsTests(DocsPostgreSqlFixture fixture) : IClassFixture<DocsPostgreSqlFixture>
{
    [DocsDockerFact]
    public async Task Migration_EnablesTenantPoliciesOnDocumentsAndVersions()
    {
        var options = new DbContextOptionsBuilder<DocsDbContext>()
            .UseNpgsql(ConnectionString(), postgres => postgres.MigrationsHistoryTable("__ef_migrations_history", DocsDbContext.Schema))
            .UseSnakeCaseNamingConvention().Options;
        await using (var context = new DocsDbContext(options, new FakeTenantContext()))
            await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT tablename, policyname FROM pg_policies WHERE schemaname = 'docs'";
        var policies = new HashSet<(string Table, string Name)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            policies.Add((reader.GetString(0), reader.GetString(1)));

        Assert.Contains(("documents", "ti_docs_documents"), policies);
        Assert.Contains(("document_versions", "ti_docs_document_versions"), policies);
    }

    private string ConnectionString() => fixture.ConnectionString;

    private sealed class FakeTenantContext : ITenantContext
    {
        public bool HasTenant => false;
        public TenantId Current => throw new InvalidOperationException("No tenant is set.");
    }
}
