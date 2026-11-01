using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Core.Domain.PurchaseRequests;
using Construction.Procurement.Core.Domain.PurchaseOrders;
using Construction.Procurement.Core.Domain.Receipts;
using Construction.Procurement.Core.Domain.Invoices;
using Construction.Procurement.Core.Domain.Outbox;
using Construction.Procurement.Core.Domain.RequestsForQuotation;
using Construction.Procurement.Core.Domain.Suppliers;
using Construction.Procurement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Construction.Procurement.Integration.Tests;

public class ProcurementModelTests
{
    [Fact]
    public void Model_MapsProcurementEntitiesToTenantScopedTables()
    {
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy;Username=dummy;Password=dummy")
            .UseSnakeCaseNamingConvention()
            .Options;

        using var context = new ProcurementDbContext(options, new FakeTenantContext());
        var request = context.Model.FindEntityType(typeof(PurchaseRequest))!;
        var item = context.Model.FindEntityType(typeof(PurchaseRequestItem))!;

        Assert.Equal("procurement", request.GetSchema());
        Assert.Equal("purchase_requests", request.GetTableName());
        Assert.Equal("procurement", item.GetSchema());
        Assert.Equal("purchase_request_items", item.GetTableName());
        Assert.False(request.FindProperty(nameof(PurchaseRequest.TenantId))!.IsNullable);
        Assert.False(item.FindProperty(nameof(PurchaseRequestItem.TenantId))!.IsNullable);
        Assert.Equal("numeric(18,4)", item.FindProperty(nameof(PurchaseRequestItem.QuantityAmount))!.GetColumnType());
        Assert.Equal("quantity_amount", item.FindProperty(nameof(PurchaseRequestItem.QuantityAmount))!.GetColumnName());

        var itemForeignKey = Assert.Single(item.GetForeignKeys(),
            key => key.PrincipalEntityType.ClrType == typeof(PurchaseRequest));
        Assert.True(itemForeignKey.IsRequired);
        Assert.Equal(
            new[] { nameof(PurchaseRequestItem.TenantId), nameof(PurchaseRequestItem.PurchaseRequestId) },
            itemForeignKey.Properties.Select(property => property.Name));

        var tenantScopedEntities = new[]
        {
            typeof(Supplier), typeof(RequestForQuotation), typeof(RequestForQuotationInvitation), typeof(SupplierQuote),
            typeof(SupplierQuoteLine), typeof(ProjectBudget), typeof(PurchaseOrder), typeof(PurchaseOrderLine),
            typeof(PurchaseOrderRevision), typeof(PurchaseOrderRevisionLine),
            typeof(GoodsReceipt), typeof(GoodsReceiptLine), typeof(SupplierInvoice), typeof(SupplierInvoiceLine),
            typeof(ProcurementOutboxMessage),
        };
        foreach (var entityType in tenantScopedEntities)
        {
            var entity = context.Model.FindEntityType(entityType)!;
            Assert.Equal("procurement", entity.GetSchema());
            Assert.False(entity.FindProperty("TenantId")!.IsNullable);
        }
    }

    [Fact]
    public async Task QueryFilter_RestrictsRequestsAndItemsToActiveTenant()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        await using (var seed = new ProcurementDbContext(options, new FakeTenantContext()))
        {
            seed.PurchaseRequests.AddRange(CreateRequest(tenantA), CreateRequest(tenantB));
            await seed.SaveChangesAsync();
        }

        await using var query = new ProcurementDbContext(options, new FakeTenantContext(tenantA));
        var result = await query.PurchaseRequests
            .Include(request => request.Items)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal(tenantA, result[0].TenantId);
        Assert.Single(result[0].Items);
        Assert.All(result[0].Items, item => Assert.Equal(tenantA, item.TenantId));
    }

    [Fact]
    public async Task RfqPersistence_ReloadsInvitationsAndManualQuoteLines()
    {
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var tenant = TenantId.New();
        var request = PurchaseRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), tenant, DateTimeOffset.UtcNow);
        request.AddItem(new PurchaseRequestItem("Steel", new Quantity(5m, "TON")));
        request.Submit();
        request.Approve("approval");
        var supplier = Supplier.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), "Steel Supplier");

        await using (var seed = new ProcurementDbContext(options, new FakeTenantContext()))
        {
            seed.PurchaseRequests.Add(request);
            seed.Suppliers.Add(supplier);
            await seed.SaveChangesAsync();
            var rfq = RequestForQuotation.Create(Guid.NewGuid(), tenant, request);
            rfq.InviteSupplier(supplier.Id);
            rfq.Issue();
            rfq.RecordQuote(supplier.Id, request.Items,
                [new SupplierQuoteLine(request.Items[0].Id, "Steel", new Quantity(5m, "TON"), new Money(90m))],
                DateTimeOffset.UtcNow);
            seed.RequestsForQuotation.Add(rfq);
            await seed.SaveChangesAsync();
        }

        await using var query = new ProcurementDbContext(options, new FakeTenantContext(tenant));
        var repository = new ProcurementRepository(query);
        var loaded = await repository.GetRequestForQuotationAsync(
            await query.RequestsForQuotation.Select(item => item.Id).SingleAsync(), tenant);

        Assert.NotNull(loaded);
        Assert.Single(loaded.InvitedSupplierIds);
        Assert.Equal(90m, Assert.Single(loaded.CompareQuotes()).Lines[0].UnitPrice);
    }

    [Fact]
    public async Task PurchaseOrderRevisionSnapshots_AreStoredAsSeparateHistoryRows()
    {
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var tenant = TenantId.New();
        var request = PurchaseRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), tenant, DateTimeOffset.UtcNow);
        var requestItem = new PurchaseRequestItem("Pipes", new Quantity(2m, "EA"));
        request.AddItem(requestItem);
        request.Submit();
        request.Approve("request-approval");
        var supplier = Supplier.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), "Pipe Supplier");
        var budget = ProjectBudget.Create(Guid.NewGuid(), tenant, request.ProjectId, new Money(300m));
        var order = PurchaseOrder.CreateFromApprovedRequest(Guid.NewGuid(), "PO-REV-1", request, supplier.Id,
            [new PurchaseOrderLine(requestItem.Id, requestItem.Description, requestItem.Quantity, new Money(100m))]);

        await using (var seed = new ProcurementDbContext(options, new FakeTenantContext()))
        {
            seed.PurchaseRequests.Add(request);
            seed.Suppliers.Add(supplier);
            seed.ProjectBudgets.Add(budget);
            order.SubmitForApproval(budget);
            order.Approve("order-approval-1");
            order.Issue(DateTimeOffset.UtcNow);
            seed.PurchaseOrders.Add(order);
            await seed.SaveChangesAsync();

            order.ReleaseBudgetReservation(budget);
            order.ReplaceLines([new PurchaseOrderLine(requestItem.Id, requestItem.Description,
                requestItem.Quantity, new Money(120m))]);
            order.SubmitForApproval(budget);
            order.Approve("order-approval-2");
            order.Issue(DateTimeOffset.UtcNow);
            await seed.SaveChangesAsync();
        }

        await using var query = new ProcurementDbContext(options, new FakeTenantContext(tenant));
        var repository = new ProcurementRepository(query);
        var loaded = await repository.GetPurchaseOrderAsync(order.Id, tenant);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Revisions.Count);
        Assert.Equal(200m, loaded.Revisions[0].TotalAmount);
        Assert.Equal(240m, loaded.Revisions[1].TotalAmount);
        Assert.Equal(100m, loaded.Revisions[0].Lines[0].UnitPriceAmount);
        Assert.Equal(120m, loaded.Revisions[1].Lines[0].UnitPriceAmount);
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
