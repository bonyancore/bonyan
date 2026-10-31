using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Construction.Kernel.Users;
using Construction.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Construction.Persistence.Tests;

public class AuditInterceptorTests
{
    private class TestAuditableEntity : IAuditableEntity, ITenantScopedEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public TenantId TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }

    private class TestSoftDeletableEntity : IAuditableEntity, ITenantScopedEntity, ISoftDeletable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public TenantId TenantId { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }

    private class FakeTenantContext : ITenantContext
    {
        public bool HasTenant { get; set; }
        public TenantId Current { get; set; }
    }

    private class FakeUserContext : IUserContext
    {
        public bool IsAuthenticated { get; set; }
        public Guid? UserId { get; set; }
    }

    private class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private class TestDbContext : DbContext
    {
        private readonly AuditSaveChangesInterceptor _interceptor;

        public DbSet<TestAuditableEntity> AuditableEntities => Set<TestAuditableEntity>();
        public DbSet<TestSoftDeletableEntity> SoftDeletableEntities => Set<TestSoftDeletableEntity>();

        public TestDbContext(DbContextOptions<TestDbContext> options, AuditSaveChangesInterceptor interceptor)
            : base(options)
        {
            _interceptor = interceptor;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(_interceptor);
            base.OnConfiguring(optionsBuilder);
        }
    }

    private static (TestDbContext Context, FakeTenantContext Tenant, FakeUserContext User, FakeClock Clock) CreateTestContext()
    {
        var tenant = new FakeTenantContext();
        var user = new FakeUserContext();
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
        var interceptor = new AuditSaveChangesInterceptor(tenant, user, clock);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new TestDbContext(options, interceptor);
        return (context, tenant, user, clock);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Added_WithoutActiveTenant_ThrowsInvalidOperationException(bool useAsync)
    {
        var (ctx, tenant, _, _) = CreateTestContext();
        tenant.HasTenant = false;

        ctx.AuditableEntities.Add(new TestAuditableEntity { Name = "Item1" });

        if (useAsync)
            await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
        else
            Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void Added_WithInvalidActiveTenant_ThrowsInvalidOperationException()
    {
        var (ctx, tenant, _, _) = CreateTestContext();
        tenant.HasTenant = true;
        tenant.Current = default(TenantId);

        ctx.AuditableEntities.Add(new TestAuditableEntity { Name = "Item1" });

        Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void Added_OverwritesCallerProvidedTenantId()
    {
        var (ctx, tenant, _, _) = CreateTestContext();
        var activeTenant = TenantId.New();
        tenant.HasTenant = true;
        tenant.Current = activeTenant;

        var entity = new TestAuditableEntity
        {
            Name = "Item1",
            TenantId = TenantId.New() // Caller attempts different tenant
        };

        ctx.AuditableEntities.Add(entity);
        ctx.SaveChanges();

        Assert.Equal(activeTenant, entity.TenantId);
    }

    [Fact]
    public void Added_SetsCreatedAndUpdatedTimestamps_AndUserContext()
    {
        var (ctx, tenant, user, clock) = CreateTestContext();
        tenant.HasTenant = true;
        tenant.Current = TenantId.New();

        var userId = Guid.NewGuid();
        user.IsAuthenticated = true;
        user.UserId = userId;

        var entity = new TestAuditableEntity { Name = "Item1" };
        ctx.AuditableEntities.Add(entity);
        ctx.SaveChanges();

        Assert.Equal(clock.UtcNow.UtcDateTime, entity.CreatedAt);
        Assert.Equal(clock.UtcNow.UtcDateTime, entity.UpdatedAt);
        Assert.Equal(userId, entity.CreatedBy);
        Assert.Equal(userId, entity.UpdatedBy);
    }

    [Fact]
    public void Added_WhenUnauthenticated_CreatedByIsNull()
    {
        var (ctx, tenant, user, _) = CreateTestContext();
        tenant.HasTenant = true;
        tenant.Current = TenantId.New();

        user.IsAuthenticated = false;
        user.UserId = null;

        var entity = new TestAuditableEntity { Name = "Item1" };
        ctx.AuditableEntities.Add(entity);
        ctx.SaveChanges();

        Assert.Null(entity.CreatedBy);
        Assert.Null(entity.UpdatedBy);
    }

    [Fact]
    public void Added_WhenAuthenticatedWithNullUserId_ThrowsInvalidOperationException()
    {
        var (ctx, tenant, user, _) = CreateTestContext();
        tenant.HasTenant = true;
        tenant.Current = TenantId.New();

        user.IsAuthenticated = true;
        user.UserId = null;

        ctx.AuditableEntities.Add(new TestAuditableEntity { Name = "Item1" });

        var ex = Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
        Assert.Contains("IUserContext contract violation", ex.Message);
    }

    [Fact]
    public void Added_WhenUnauthenticatedWithSpoofedUserId_CreatedByIsNull()
    {
        var (ctx, tenant, user, _) = CreateTestContext();
        tenant.HasTenant = true;
        tenant.Current = TenantId.New();

        user.IsAuthenticated = false;
        user.UserId = Guid.NewGuid(); // Spoofed

        var entity = new TestAuditableEntity { Name = "Item1" };
        ctx.AuditableEntities.Add(entity);
        ctx.SaveChanges();

        Assert.Null(entity.CreatedBy);
        Assert.Null(entity.UpdatedBy);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Modified_WithoutActiveTenant_ThrowsInvalidOperationException(bool useAsync)
    {
        var (ctx, tenant, _, _) = CreateTestContext();
        var tId = TenantId.New();
        tenant.HasTenant = true;
        tenant.Current = tId;

        var entity = new TestAuditableEntity { Name = "Item1" };
        ctx.AuditableEntities.Add(entity);
        ctx.SaveChanges();

        // Switch tenant context to no tenant
        tenant.HasTenant = false;
        entity.Name = "Item1-Updated";

        if (useAsync)
            await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
        else
            Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void Modified_WithInvalidActiveTenant_ThrowsInvalidOperationException()
    {
        var (ctx, tenant, _, _) = CreateTestContext();
        var tId = TenantId.New();
        tenant.HasTenant = true;
        tenant.Current = tId;

        var entity = new TestAuditableEntity { Name = "Item1" };
        ctx.AuditableEntities.Add(entity);
        ctx.SaveChanges();

        tenant.Current = default(TenantId);
        entity.Name = "Item1-Updated";

        Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void Modified_WithInvalidEntityTenant_ThrowsInvalidOperationException()
    {
        var (ctx, tenant, _, _) = CreateTestContext();
        var tId = TenantId.New();
        tenant.HasTenant = true;
        tenant.Current = tId;

        var entity = new TestAuditableEntity { Name = "Item1" };
        ctx.AuditableEntities.Add(entity);
        ctx.SaveChanges();

        // Tamper entity to default
        entity.TenantId = default(TenantId);
        entity.Name = "Item1-Updated";

        Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void Modified_WithDifferentTenant_ThrowsInvalidOperationException()
    {
        var (ctx, tenant, _, _) = CreateTestContext();
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        tenant.HasTenant = true;
        tenant.Current = tenantA;

        var entity = new TestAuditableEntity { Name = "Item1" };
        ctx.AuditableEntities.Add(entity);
        ctx.SaveChanges();

        // Active tenant becomes Tenant B
        tenant.Current = tenantB;
        entity.Name = "Item1-Updated";

        var ex = Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
        Assert.Contains("Tenant mismatch", ex.Message);
    }

    [Fact]
    public void Modified_AttemptedTenantIdChange_ThrowsInvalidOperationException()
    {
        var (ctx, tenant, _, _) = CreateTestContext();
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        tenant.HasTenant = true;
        tenant.Current = tenantA;

        var entity = new TestAuditableEntity { Name = "Item1" };
        ctx.AuditableEntities.Add(entity);
        ctx.SaveChanges();

        // Attempt to mutate TenantId on tracked entity
        entity.TenantId = tenantB;

        var ex = Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
        Assert.Contains("TenantId mutation rejected", ex.Message);
    }

    [Fact]
    public void Modified_TenantIdChangedAndRevertedBeforeSave_Succeeds()
    {
        var (ctx, tenant, _, _) = CreateTestContext();
        var tenantA = TenantId.New();

        tenant.HasTenant = true;
        tenant.Current = tenantA;

        var entity = new TestAuditableEntity { Name = "Item1" };
        ctx.AuditableEntities.Add(entity);
        ctx.SaveChanges();

        // Change and revert
        entity.TenantId = TenantId.New();
        entity.TenantId = tenantA;
        entity.Name = "Item1-Updated";

        ctx.SaveChanges();
        Assert.Equal(tenantA, entity.TenantId);
    }

    [Fact]
    public void Modified_LocksCreatedAtAndCreatedBy_AndUpdatesUpdatedAtAndUpdatedBy()
    {
        var (ctx, tenant, user, clock) = CreateTestContext();
        tenant.HasTenant = true;
        tenant.Current = TenantId.New();

        var initialUser = Guid.NewGuid();
        user.IsAuthenticated = true;
        user.UserId = initialUser;

        var entity = new TestAuditableEntity { Name = "Item1" };
        ctx.AuditableEntities.Add(entity);
        ctx.SaveChanges();

        var originalCreatedAt = entity.CreatedAt;
        var originalCreatedBy = entity.CreatedBy;

        // Advance clock and change user
        clock.UtcNow = clock.UtcNow.AddHours(2);
        var secondUser = Guid.NewGuid();
        user.UserId = secondUser;

        // Attempt tampering with CreatedAt/CreatedBy
        entity.CreatedAt = DateTime.UtcNow.AddDays(-10);
        entity.CreatedBy = Guid.NewGuid();
        entity.Name = "Item1-Modified";

        ctx.SaveChanges();

        // Assert audit stampings
        Assert.Equal(originalCreatedAt, entity.CreatedAt);
        Assert.Equal(originalCreatedBy, entity.CreatedBy);
        Assert.Equal(clock.UtcNow.UtcDateTime, entity.UpdatedAt);
        Assert.Equal(secondUser, entity.UpdatedBy);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Deleted_WithoutActiveTenant_ThrowsInvalidOperationException(bool useAsync)
    {
        var (ctx, tenant, _, _) = CreateTestContext();
        tenant.HasTenant = true;
        tenant.Current = TenantId.New();

        var entity = new TestAuditableEntity { Name = "Item1" };
        ctx.AuditableEntities.Add(entity);
        ctx.SaveChanges();

        tenant.HasTenant = false;
        ctx.AuditableEntities.Remove(entity);

        if (useAsync)
            await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
        else
            Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void Deleted_WithDifferentTenant_ThrowsInvalidOperationException()
    {
        var (ctx, tenant, _, _) = CreateTestContext();
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        tenant.HasTenant = true;
        tenant.Current = tenantA;

        var entity = new TestAuditableEntity { Name = "Item1" };
        ctx.AuditableEntities.Add(entity);
        ctx.SaveChanges();

        tenant.Current = tenantB;
        ctx.AuditableEntities.Remove(entity);

        var ex = Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
        Assert.Contains("Tenant mismatch", ex.Message);
    }

    [Fact]
    public void SoftDelete_TreatedAsModified_PassesTenantGuard()
    {
        var (ctx, tenant, user, clock) = CreateTestContext();
        var tenantA = TenantId.New();

        tenant.HasTenant = true;
        tenant.Current = tenantA;

        var entity = new TestSoftDeletableEntity();
        ctx.SoftDeletableEntities.Add(entity);
        ctx.SaveChanges();

        // Soft delete is an update
        clock.UtcNow = clock.UtcNow.AddHours(1);
        entity.IsDeleted = true;

        ctx.SaveChanges();

        Assert.True(entity.IsDeleted);
        Assert.Equal(clock.UtcNow.UtcDateTime, entity.UpdatedAt);
    }
}

