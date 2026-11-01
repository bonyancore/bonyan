using Construction.Docs.Core.Domain.Documents;
using Construction.Docs.Infrastructure.Persistence;
using Construction.Kernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Construction.Docs.Integration.Tests;

public sealed class DocsModelTests
{
    [Fact]
    public void Model_MapsTenantScopedDocumentAndVersionTables()
    {
        var options = new DbContextOptionsBuilder<DocsDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy;Username=dummy;Password=dummy")
            .UseSnakeCaseNamingConvention().Options;
        using var context = new DocsDbContext(options, new FakeTenantContext());
        var document = context.Model.FindEntityType(typeof(DocumentRecord))!;
        var version = context.Model.FindEntityType(typeof(DocumentVersion))!;

        Assert.Equal("docs", document.GetSchema());
        Assert.Equal("documents", document.GetTableName());
        Assert.Equal("docs", version.GetSchema());
        Assert.Equal("document_versions", version.GetTableName());
        Assert.False(document.FindProperty("TenantId")!.IsNullable);
        Assert.False(version.FindProperty("TenantId")!.IsNullable);
        Assert.Contains(version.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(DocumentRecord));
    }

    [Fact]
    public async Task QueryFilter_OnlyReturnsDocumentsForActiveTenant()
    {
        var options = new DbContextOptionsBuilder<DocsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();
        await using (var seed = new DocsDbContext(options, new FakeTenantContext()))
        {
            seed.Documents.AddRange(Create(tenantA), Create(tenantB));
            await seed.SaveChangesAsync();
        }

        await using var query = new DocsDbContext(options, new FakeTenantContext(tenantA));
        var results = await query.Documents.Include(item => item.Versions).ToListAsync();

        Assert.Single(results);
        Assert.Equal(tenantA, results[0].TenantId);
        Assert.Single(results[0].Versions);
    }

    private static DocumentRecord Create(TenantId tenant) => DocumentRecord.Create(
        Guid.NewGuid(), tenant, "procurement.purchase-order", Guid.NewGuid(), Guid.NewGuid(), "Order PDF");

    private sealed class FakeTenantContext(TenantId? tenant = null) : ITenantContext
    {
        public bool HasTenant => tenant.HasValue;
        public TenantId Current => tenant ?? throw new InvalidOperationException("No tenant is set.");
    }
}
