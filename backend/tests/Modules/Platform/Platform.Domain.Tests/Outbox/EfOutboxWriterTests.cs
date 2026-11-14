using Construction.Kernel.Tenancy;
using Construction.Messaging;
using Construction.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Construction.Platform.Domain.Tests.Outbox;

public sealed class EfOutboxWriterTests
{
    [EventType("test.item.created.v1")]
    private sealed record ItemCreated(Guid AggregateId, long AggregateVersion, string Name);

    private sealed class TestOutboxDbContext : DbContext
    {
        public TestOutboxDbContext(DbContextOptions options) : base(options) { }

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OutboxMessage>(b =>
            {
                b.ToTable("outbox");
                b.HasKey(x => x.Id);
                b.Property(x => x.Payload).HasColumnType("TEXT");
            });
        }
    }

    private static (TestOutboxDbContext Db, EfOutboxWriter Writer, AsyncLocalTenantContext Tenant) CreateSut(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<TestOutboxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new TestOutboxDbContext(options);
        var tenant = new AsyncLocalTenantContext();
        tenant.SetTenant(new TenantId(tenantId));
        var writer = new EfOutboxWriter(db, tenant);
        return (db, writer, tenant);
    }

    [Fact]
    public async Task Publish_ThenSaveChanges_PersistsExactlyOneRow()
    {
        var tenantId = Guid.CreateVersion7();
        var (db, writer, _) = CreateSut(tenantId);
        var aggregateId = Guid.CreateVersion7();

        writer.Publish(new ItemCreated(aggregateId, 1, "Alpha"));
        await db.SaveChangesAsync();

        var rows = await db.OutboxMessages.ToListAsync();
        Assert.Single(rows);
        Assert.Equal(tenantId, rows[0].TenantId);
        Assert.Equal(aggregateId, rows[0].AggregateId);
        Assert.Equal(1, rows[0].AggregateVersion);
        Assert.Equal("test.item.created.v1", rows[0].Type);
        Assert.Contains("Alpha", rows[0].Payload);
        Assert.Null(rows[0].DispatchedAt);
    }

    [Fact]
    public async Task Publish_WithoutSaveChanges_LeavesNoPersistedRow()
    {
        var tenantId = Guid.CreateVersion7();
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<TestOutboxDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        await using (var db = new TestOutboxDbContext(options))
        {
            var tenant = new AsyncLocalTenantContext();
            tenant.SetTenant(new TenantId(tenantId));
            var writer = new EfOutboxWriter(db, tenant);

            writer.Publish(new ItemCreated(Guid.CreateVersion7(), 1, "RollbackMe"));
            // Simulate business transaction rollback: discard tracked changes without SaveChanges.
            Assert.Single(db.ChangeTracker.Entries<OutboxMessage>());
        }

        await using var verify = new TestOutboxDbContext(options);
        Assert.Empty(await verify.OutboxMessages.ToListAsync());
    }

    [Fact]
    public void Publish_WithoutTenantContext_Throws()
    {
        var options = new DbContextOptionsBuilder<TestOutboxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new TestOutboxDbContext(options);
        var tenant = new AsyncLocalTenantContext();
        var writer = new EfOutboxWriter(db, tenant);

        Assert.Throws<InvalidOperationException>(() =>
            writer.Publish(new ItemCreated(Guid.CreateVersion7(), 1, "NoTenant")));
    }

    [Fact]
    public async Task Publish_Envelope_UsesEventIdAsOutboxId()
    {
        var tenantId = Guid.CreateVersion7();
        var (db, writer, _) = CreateSut(tenantId);
        var payload = new ItemCreated(Guid.CreateVersion7(), 3, "Env");
        var envelope = EventEnvelope<ItemCreated>.Create(payload, tenantId);

        writer.Publish(envelope);
        await db.SaveChangesAsync();

        var row = Assert.Single(await db.OutboxMessages.ToListAsync());
        Assert.Equal(envelope.EventId, row.Id);
        Assert.Equal(3, row.AggregateVersion);
    }
}
