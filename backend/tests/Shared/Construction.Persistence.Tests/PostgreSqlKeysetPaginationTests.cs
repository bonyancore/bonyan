using System.Data.Common;
using System.Diagnostics;
using Construction.Kernel.Pagination;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Construction.Persistence.Pagination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Construction.Persistence.Tests.Integration;

public class PostgreSqlKeysetFixture : IAsyncLifetime
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
            .WithDatabase("keyset_test_db")
            .WithUsername("postgres")
            .WithPassword("postgres_dev")
            .Build();
#pragma warning restore CS0618

        await Container.StartAsync();

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS test_keyset_entities (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                name text NOT NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NOT NULL,
                updated_by uuid NULL
            );

            CREATE INDEX IF NOT EXISTS ix_test_keyset_entities_seek
                ON test_keyset_entities (created_at DESC, id DESC);
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

public class CommandInterceptorSqlCapture : DbCommandInterceptor
{
    public List<string> ExecutedSqlCommands { get; } = new();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        ExecutedSqlCommands.Add(command.CommandText);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ExecutedSqlCommands.Add(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

public class PostgreSqlKeysetPaginationTests(PostgreSqlKeysetFixture fixture)
    : IClassFixture<PostgreSqlKeysetFixture>
{
    public class KeysetTestEntity : ITenantScopedEntity, IAuditableEntity
    {
        public Guid Id { get; set; }
        public TenantId TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }

    private class KeysetDbContext(
        DbContextOptions<KeysetDbContext> options,
        ITenantContext tenantContext)
        : ModuleDbContext(options, "public", tenantContext)
    {
        public DbSet<KeysetTestEntity> KeysetEntities => Set<KeysetTestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<KeysetTestEntity>(b =>
            {
                b.ToTable("test_keyset_entities", "public");
                b.HasKey(e => e.Id);
                b.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");
                b.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");
            });
        }
    }

    private class FakeTenantContext : ITenantContext
    {
        public bool HasTenant { get; set; } = true;
        public TenantId Current { get; set; } = TenantId.New();
    }

    [Fact]
    public async Task PageSize_Validation_RejectsValuesOutsideBounds()
    {
        var dummyQuery = Enumerable.Empty<KeysetTestEntity>().AsQueryable();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            dummyQuery.ToKeysetPageAsync(e => e.Id, null, 0, e => e));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            dummyQuery.ToKeysetPageAsync(e => e.Id, null, -1, e => e));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            dummyQuery.ToKeysetPageAsync(e => e.Id, null, 201, e => e));
    }

    [DockerFact]
    public async Task KeysetPagination_FullTraverse_NoDuplicates_NoOmissions_SqlVerified()
    {
        var tenant = TenantId.New();
        var tenantContext = new FakeTenantContext { Current = tenant, HasTenant = true };

        var captureInterceptor = new CommandInterceptorSqlCapture();
        var options = new DbContextOptionsBuilder<KeysetDbContext>()
            .UseNpgsql(fixture.ConnectionString).UseSnakeCaseNamingConvention()
            .AddInterceptors(captureInterceptor)
            .Options;

        await using var db = new KeysetDbContext(options, tenantContext);

        // Seed 15 records across 3 distinct timestamps (5 records each to test timestamp ties).
        // Include UUID values with diverse byte / string / lexical layouts.
        var baseTime = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

        var seedRecords = new List<KeysetTestEntity>();
        var seededUuids = new List<Guid>
        {
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("01933088-0000-7000-8000-000000000001"),
            Guid.Parse("01933088-0000-7000-8000-000000000002"),
            Guid.Parse("0fffffff-ffff-ffff-ffff-ffffffffffff"),
            Guid.Parse("7fffffff-ffff-ffff-ffff-ffffffffffff"),

            Guid.Parse("80000000-0000-0000-0000-000000000001"),
            Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            Guid.Parse("b0000000-0000-0000-0000-000000000001"),
            Guid.Parse("c0000000-0000-0000-0000-000000000001"),
            Guid.Parse("efffffff-ffff-ffff-ffff-ffffffffffff"),

            Guid.Parse("f0000000-0000-0000-0000-000000000001"),
            Guid.Parse("f0000000-0000-0000-0000-000000000002"),
            Guid.Parse("fe000000-0000-0000-0000-000000000001"),
            Guid.Parse("ff000000-0000-0000-0000-000000000001"),
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")
        };

        for (int i = 0; i < 15; i++)
        {
            // Group into 3 timestamps: 0..4 share time + 2 hrs, 5..9 share time + 1 hr, 10..14 share time
            var timeOffset = (2 - (i / 5)); // 2, 1, or 0
            var timestamp = baseTime.AddHours(timeOffset);

            seedRecords.Add(new KeysetTestEntity
            {
                Id = seededUuids[i],
                TenantId = tenant,
                Name = $"Entity-{i}",
                CreatedAt = timestamp,
                UpdatedAt = timestamp
            });
        }

        db.KeysetEntities.AddRange(seedRecords);
        await db.SaveChangesAsync();

        // Traverse all pages with pageSize = 3
        const int pageSize = 3;
        string? cursor = null;
        var collectedIds = new List<Guid>();
        var pageCount = 0;

        do
        {
            var page = await db.KeysetEntities
                .AsNoTracking()
                .ToKeysetPageAsync(e => e.Id, cursor, pageSize, e => e);

            pageCount++;
            Assert.True(page.Items.Count <= pageSize);
            collectedIds.AddRange(page.Items.Select(x => x.Id));

            cursor = page.NextCursor;
        } while (cursor is not null);

        // Verification: Exactly 15 items fetched across 5 pages
        Assert.Equal(15, collectedIds.Count);
        Assert.Equal(5, pageCount);

        // Verification: Zero duplicates and zero omissions
        var distinctIds = collectedIds.Distinct().ToList();
        Assert.Equal(15, distinctIds.Count);
        Assert.Equal(seededUuids.OrderBy(u => u).ToList(), distinctIds.OrderBy(u => u).ToList());

        // Capture SQL and verify ordering & seek predicate translation
        var sqlCommands = captureInterceptor.ExecutedSqlCommands;
        Assert.NotEmpty(sqlCommands);

        // Ensure at least one query contains the seek predicate generated by Npgsql EF Core translation
        var paginatedQuerySql = sqlCommands.LastOrDefault(s => s.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(paginatedQuerySql);

        // Verify ORDER BY created_at DESC, id DESC
        Assert.Contains("ORDER BY", paginatedQuerySql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("created_at", paginatedQuerySql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DESC", paginatedQuerySql, StringComparison.OrdinalIgnoreCase);

        // Subsequent page queries must translate the seek predicate:
        // (created_at < seek.CreatedAt) OR (created_at == seek.CreatedAt AND id < seek.Id)
        // Subsequent page queries must translate the seek predicate:
        // (created_at < seek.CreatedAt) OR (created_at == seek.CreatedAt AND id < seek.Id)
        var subsequentQuery = sqlCommands.FirstOrDefault(s =>
            s.Contains("created_at < TIMESTAMPTZ", StringComparison.OrdinalIgnoreCase) &&
            s.Contains("AND t.id <", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(subsequentQuery);
        Assert.Contains("created_at < TIMESTAMPTZ", subsequentQuery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AND t.id <", subsequentQuery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY t.created_at DESC, t.id DESC", subsequentQuery, StringComparison.OrdinalIgnoreCase);
    }

    [DockerFact]
    public async Task KeysetPagination_CancellationToken_AbortsExecution()
    {
        var tenant = TenantId.New();
        var tenantContext = new FakeTenantContext { Current = tenant, HasTenant = true };

        var options = new DbContextOptionsBuilder<KeysetDbContext>()
            .UseNpgsql(fixture.ConnectionString).UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new KeysetDbContext(options, tenantContext);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled token

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await db.KeysetEntities
                .AsNoTracking()
                .ToKeysetPageAsync(e => e.Id, null, 10, e => e, cts.Token);
        });
    }

    [DockerFact]
    public async Task KeysetPagination_NullCursor_ReturnsFirstPage()
    {
        var tenant = TenantId.New();
        var tenantContext = new FakeTenantContext { Current = tenant, HasTenant = true };
        var options = new DbContextOptionsBuilder<KeysetDbContext>()
            .UseNpgsql(fixture.ConnectionString).UseSnakeCaseNamingConvention().Options;
        await using var db = new KeysetDbContext(options, tenantContext);

        var now = DateTime.UtcNow;
        db.KeysetEntities.Add(new KeysetTestEntity
        {
            TenantId = tenant,
            Name = "NullCursor-A",
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        // null afterCursor → fetch first page, no exception
        var page = await db.KeysetEntities.AsNoTracking()
            .ToKeysetPageAsync(e => e.Id, null, 10, e => e);

        Assert.True(page.Items.Count >= 1);
    }

    [DockerFact]
    public async Task KeysetPagination_EmptyOrWhitespaceCursor_ThrowsArgumentException()
    {
        var tenant = TenantId.New();
        var tenantContext = new FakeTenantContext { Current = tenant, HasTenant = true };
        var options = new DbContextOptionsBuilder<KeysetDbContext>()
            .UseNpgsql(fixture.ConnectionString).UseSnakeCaseNamingConvention().Options;
        await using var db = new KeysetDbContext(options, tenantContext);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            db.KeysetEntities.AsNoTracking()
                .ToKeysetPageAsync(e => e.Id, "", 10, e => e));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            db.KeysetEntities.AsNoTracking()
                .ToKeysetPageAsync(e => e.Id, "   ", 10, e => e));
    }

    [DockerFact]
    public async Task KeysetPagination_MalformedCursor_ThrowsArgumentException()
    {
        var tenant = TenantId.New();
        var tenantContext = new FakeTenantContext { Current = tenant, HasTenant = true };
        var options = new DbContextOptionsBuilder<KeysetDbContext>()
            .UseNpgsql(fixture.ConnectionString).UseSnakeCaseNamingConvention().Options;
        await using var db = new KeysetDbContext(options, tenantContext);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            db.KeysetEntities.AsNoTracking()
                .ToKeysetPageAsync(e => e.Id, "not-a-valid-cursor", 10, e => e));
    }

    [DockerFact]
    public async Task KeysetPagination_MicrosecondPrecision_PreservesSeekOrdering()
    {
        var tenant = TenantId.New();
        var tenantContext = new FakeTenantContext { Current = tenant, HasTenant = true };
        var options = new DbContextOptionsBuilder<KeysetDbContext>()
            .UseNpgsql(fixture.ConnectionString).UseSnakeCaseNamingConvention().Options;
        await using var db = new KeysetDbContext(options, tenantContext);

        // Create base time truncated to microsecond precision (PostgreSQL timestamptz has 1 µs resolution)
        var rawTicks = DateTime.UtcNow.Ticks;
        var baseTime = new DateTime(rawTicks - (rawTicks % 10), DateTimeKind.Utc);

        // Three entities: same second, differ by 1 microsecond each (10 ticks = 1 µs)
        var t1 = baseTime;
        var t2 = baseTime.AddTicks(10);  // +1 µs
        var t3 = baseTime.AddTicks(20);  // +2 µs

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        db.KeysetEntities.AddRange(
            new KeysetTestEntity { Id = id1, TenantId = tenant, Name = "µs-E1", CreatedAt = t1, UpdatedAt = t1 },
            new KeysetTestEntity { Id = id2, TenantId = tenant, Name = "µs-E2", CreatedAt = t2, UpdatedAt = t2 },
            new KeysetTestEntity { Id = id3, TenantId = tenant, Name = "µs-E3", CreatedAt = t3, UpdatedAt = t3 }
        );
        await db.SaveChangesAsync();

        // Pages are ordered (created_at DESC, id DESC): t3 → t2 → t1
        var page1 = await db.KeysetEntities.AsNoTracking()
            .Where(e => e.Id == id1 || e.Id == id2 || e.Id == id3)
            .ToKeysetPageAsync(e => e.Id, null, 1, e => e);
        Assert.Single(page1.Items);
        Assert.Equal(id3, page1.Items[0].Id);
        Assert.NotNull(page1.NextCursor);

        var page2 = await db.KeysetEntities.AsNoTracking()
            .Where(e => e.Id == id1 || e.Id == id2 || e.Id == id3)
            .ToKeysetPageAsync(e => e.Id, page1.NextCursor, 1, e => e);
        Assert.Single(page2.Items);
        Assert.Equal(id2, page2.Items[0].Id);
        Assert.NotNull(page2.NextCursor);

        var page3 = await db.KeysetEntities.AsNoTracking()
            .Where(e => e.Id == id1 || e.Id == id2 || e.Id == id3)
            .ToKeysetPageAsync(e => e.Id, page2.NextCursor, 1, e => e);
        Assert.Single(page3.Items);
        Assert.Equal(id1, page3.Items[0].Id);
        Assert.Null(page3.NextCursor);
    }

    [DockerFact]
    public async Task KeysetPagination_ExactPostgreSqlUuidOrder_MatchesNativeDatabaseSort()
    {
        var tenant = TenantId.New();
        var tenantContext = new FakeTenantContext { Current = tenant, HasTenant = true };
        var options = new DbContextOptionsBuilder<KeysetDbContext>()
            .UseNpgsql(fixture.ConnectionString).UseSnakeCaseNamingConvention().Options;
        await using var db = new KeysetDbContext(options, tenantContext);

        // Seed 6 entities sharing the same timestamp (all tie-breaking on id)
        var rawTicks = DateTime.UtcNow.Ticks;
        var sameTime = new DateTime(rawTicks - (rawTicks % 10), DateTimeKind.Utc);

        var ids = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();
        foreach (var id in ids)
        {
            db.KeysetEntities.Add(new KeysetTestEntity
            {
                Id = id,
                TenantId = tenant,
                Name = id.ToString(),
                CreatedAt = sameTime,
                UpdatedAt = sameTime
            });
        }
        await db.SaveChangesAsync();

        // Collect all pages via keyset pagination
        string? cursor = null;
        var collectedIds = new List<Guid>();
        do
        {
            var page = await db.KeysetEntities.AsNoTracking()
                .Where(e => ids.Contains(e.Id))
                .ToKeysetPageAsync(e => e.Id, cursor, 2, e => e);
            collectedIds.AddRange(page.Items.Select(x => x.Id));
            cursor = page.NextCursor;
        } while (cursor is not null);

        Assert.Equal(6, collectedIds.Count);

        // Fetch the expected order directly from PostgreSQL using the same sort key
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        var idParams = string.Join(",", ids.Select((_, i) => $"@p{i}"));
        cmd.CommandText = $"SELECT id FROM test_keyset_entities WHERE id IN ({idParams}) ORDER BY created_at DESC, id DESC";
        for (int i = 0; i < ids.Count; i++)
            cmd.Parameters.AddWithValue($"p{i}", ids[i]);

        var expectedIds = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            expectedIds.Add(reader.GetGuid(0));

        Assert.Equal(expectedIds, collectedIds);
    }

    [DockerFact]
    public async Task KeysetPagination_WithTotal_True_CalculatesTotalCountBeforeCursor()
    {
        var tenant = TenantId.New();
        var tenantContext = new FakeTenantContext { Current = tenant, HasTenant = true };

        var captureInterceptor = new CommandInterceptorSqlCapture();
        var options = new DbContextOptionsBuilder<KeysetDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(captureInterceptor)
            .Options;

        await using var db = new KeysetDbContext(options, tenantContext);

        // Seed 10 entities
        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            db.KeysetEntities.Add(new KeysetTestEntity
            {
                TenantId = tenant,
                Name = $"Item-{i}",
                CreatedAt = now.AddMinutes(-i),
                UpdatedAt = now.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();

        // Query first page with withTotal = true
        var page1 = await db.KeysetEntities
            .AsNoTracking()
            .ToKeysetPageAsync(e => e.Id, null, 3, e => e, withTotal: true);

        Assert.Equal(3, page1.Items.Count);
        Assert.NotNull(page1.NextCursor);
        Assert.Equal(10, page1.Total);

        // Query second page with withTotal = true
        var page2 = await db.KeysetEntities
            .AsNoTracking()
            .ToKeysetPageAsync(e => e.Id, page1.NextCursor, 3, e => e, withTotal: true);

        Assert.Equal(3, page2.Items.Count);
        Assert.NotNull(page2.NextCursor);
        // Total MUST reflect the base query count (10), not remaining items
        Assert.Equal(10, page2.Total);

        // Query with withTotal = false
        var countBefore = captureInterceptor.ExecutedSqlCommands.Count(s => s.Contains("count", StringComparison.OrdinalIgnoreCase));
        var pageWithoutTotal = await db.KeysetEntities
            .AsNoTracking()
            .ToKeysetPageAsync(e => e.Id, null, 3, e => e, withTotal: false);

        var countAfter = captureInterceptor.ExecutedSqlCommands.Count(s => s.Contains("count", StringComparison.OrdinalIgnoreCase));

        Assert.Null(pageWithoutTotal.Total);
        // Assert no COUNT query was executed when withTotal is false
        Assert.Equal(countBefore, countAfter);
    }
}
