using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Contracts.Events;
using Construction.Procurement.Core.Domain.Invoices;
using Construction.Procurement.Core.Domain.PurchaseOrders;
using Construction.Procurement.Core.Domain.PurchaseRequests;
using Construction.Procurement.Core.Domain.Receipts;
using Construction.Procurement.Core.Domain.RequestsForQuotation;
using Construction.Procurement.Core.Domain.Suppliers;
using Xunit;

namespace Construction.Procurement.Domain.Tests;

public sealed class ProcurementWorkflowTests
{
    [Fact]
    public void Supplier_Create_UsesCorePartyAndNormalizesContact()
    {
        var supplier = Supplier.Create(Guid.NewGuid(), TenantId.New(), Guid.NewGuid(), "  Supplier Ltd  ", "  buyer@example.com ", " +20 100 ");

        Assert.Equal("Supplier Ltd", supplier.Name);
        Assert.Equal("buyer@example.com", supplier.Email);
        Assert.Equal("+20 100", supplier.Phone);
        Assert.Equal(SupplierStatus.Active, supplier.Status);
    }

    [Fact]
    public void RequestForQuotation_RecordsAndComparesManualQuotesThenAwardsOne()
    {
        var request = CreateApprovedRequest();
        var rfq = RequestForQuotation.Create(Guid.NewGuid(), request.TenantId, request);
        var firstSupplier = Guid.NewGuid();
        var secondSupplier = Guid.NewGuid();
        rfq.InviteSupplier(firstSupplier);
        rfq.InviteSupplier(secondSupplier);
        rfq.Issue();
        rfq.RecordQuote(firstSupplier, request.Items,
            [NewQuoteLine(request.Items[0], 125m)], DateTimeOffset.UtcNow);
        rfq.RecordQuote(secondSupplier, request.Items,
            [NewQuoteLine(request.Items[0], 110m)], DateTimeOffset.UtcNow);

        var comparison = rfq.CompareQuotes();
        var awarded = rfq.AwardQuote(secondSupplier);

        Assert.Equal(secondSupplier, comparison[0].SupplierId);
        Assert.Equal(secondSupplier, awarded.SupplierId);
        Assert.Equal(RequestForQuotationStatus.Awarded, rfq.Status);
        Assert.Equal(110m, comparison[0].Lines[0].UnitPrice);
    }

    [Fact]
    public void PurchaseOrder_ReservesBudgetIssuesAndCreatesRevisionEvent()
    {
        var request = CreateApprovedRequest();
        var supplierId = Guid.NewGuid();
        var order = CreateOrder(request, supplierId, 100m);
        var budget = ProjectBudget.Create(Guid.NewGuid(), request.TenantId, request.ProjectId, new Money(300m));

        order.SubmitForApproval(budget);
        Assert.Equal(100m, budget.ReservedAmount);
        order.Approve("approval-1");
        var issued = order.Issue(DateTimeOffset.UtcNow);
        Assert.Equal(1, issued.Revision);

        order.ReleaseBudgetReservation(budget);
        order.ReplaceLines([NewOrderLine(request.Items[0], 120m)]);
        order.SubmitForApproval(budget);
        order.Approve("approval-2");
        order.Issue(DateTimeOffset.UtcNow);
        var modified = order.GetModifiedEvent(DateTimeOffset.UtcNow);

        Assert.Equal(2, modified.Revision);
        Assert.Equal(120m, budget.ReservedAmount);
        Assert.Equal(2, order.Revisions.Count);
        Assert.Equal(100m, order.Revisions[0].TotalAmount);
        Assert.Equal(120m, order.Revisions[1].TotalAmount);
        Assert.Equal(100m, order.Revisions[0].Lines[0].UnitPriceAmount);
    }

    [Fact]
    public void PurchaseOrder_CreateRejectsQuotedQuantitiesDifferentFromApprovedRequest()
    {
        var request = CreateApprovedRequest(quantity: 10m);
        var mismatched = new PurchaseOrderLine(request.Items[0].Id, "Cement",
            new Quantity(9m, "EA"), new Money(12m));

        var exception = Assert.Throws<DomainRuleViolation>(() =>
            PurchaseOrder.CreateFromApprovedRequest(Guid.NewGuid(), "PO-TEST-2", request, Guid.NewGuid(), [mismatched]));

        Assert.Equal("procurement.po_quantity_must_match_request", exception.Code);
    }

    [Fact]
    public void RequestForQuotation_RejectsQuoteQuantityDifferentFromRequest()
    {
        var request = CreateApprovedRequest(quantity: 10m);
        var rfq = RequestForQuotation.Create(Guid.NewGuid(), request.TenantId, request);
        var supplier = Guid.NewGuid();
        rfq.InviteSupplier(supplier);
        rfq.Issue();

        var exception = Assert.Throws<DomainRuleViolation>(() => rfq.RecordQuote(supplier, request.Items,
            [new SupplierQuoteLine(request.Items[0].Id, "Cement", new Quantity(8m, "EA"), new Money(12m))],
            DateTimeOffset.UtcNow));

        Assert.Equal("procurement.rfq_quote_quantity_must_match_request", exception.Code);
    }

    [Fact]
    public void PurchaseOrder_RejectsReservationsThatWouldExceedRemainingBudget()
    {
        var request = CreateApprovedRequest();
        var budget = ProjectBudget.Create(Guid.NewGuid(), request.TenantId, request.ProjectId, new Money(150m));
        var first = CreateOrder(request, Guid.NewGuid(), 100m);
        var second = CreateOrder(request, Guid.NewGuid(), 100m);

        first.SubmitForApproval(budget);
        var exception = Assert.Throws<DomainRuleViolation>(() => second.SubmitForApproval(budget));

        Assert.Equal("procurement.po_budget_exceeded", exception.Code);
        Assert.Equal(100m, budget.ReservedAmount);
    }

    [Fact]
    public void GoodsReceipt_AllowsPartialReceiptsButRejectsQuantitiesOverOrder()
    {
        var request = CreateApprovedRequest(quantity: 10m);
        var order = CreateIssuedOrder(request, 10m);
        var orderLine = Assert.Single(order.Lines);
        var first = GoodsReceipt.Record(Guid.NewGuid(), order,
            [new GoodsReceiptLine(orderLine.Id, new Quantity(4m, "EA"))],
            new Dictionary<Guid, Quantity> { [orderLine.Id] = Quantity.Zero("EA") }, DateTimeOffset.UtcNow);
        var second = GoodsReceipt.Record(Guid.NewGuid(), order,
            [new GoodsReceiptLine(orderLine.Id, new Quantity(6m, "EA"))],
            new Dictionary<Guid, Quantity> { [orderLine.Id] = new Quantity(4m, "EA") }, DateTimeOffset.UtcNow);

        Assert.Equal(4m, first.Lines[0].Quantity.Amount);
        Assert.Equal(6m, second.Lines[0].Quantity.Amount);
        Assert.Throws<DomainRuleViolation>(() => GoodsReceipt.Record(Guid.NewGuid(), order,
            [new GoodsReceiptLine(orderLine.Id, new Quantity(1m, "EA"))],
            new Dictionary<Guid, Quantity> { [orderLine.Id] = new Quantity(10m, "EA") }, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SupplierInvoice_ThreeWayMatchDisputesUnreceivedOrMismatchedPrice()
    {
        var request = CreateApprovedRequest(quantity: 10m);
        var order = CreateIssuedOrder(request, 20m);
        var orderLine = Assert.Single(order.Lines);
        var receipt = GoodsReceipt.Record(Guid.NewGuid(), order,
            [new GoodsReceiptLine(orderLine.Id, new Quantity(4m, "EA"))],
            new Dictionary<Guid, Quantity> { [orderLine.Id] = Quantity.Zero("EA") }, DateTimeOffset.UtcNow);
        var invoice = SupplierInvoice.Create(Guid.NewGuid(), "INV-001", order,
            [new SupplierInvoiceLine(orderLine.Id, new Quantity(5m, "EA"), new Money(20m))]);

        invoice.MatchAgainst(order, [receipt], DateTimeOffset.UtcNow);

        Assert.Equal(SupplierInvoiceStatus.Disputed, invoice.Status);
        Assert.Contains("received quantity", invoice.DisputeReason, StringComparison.Ordinal);
        Assert.Throws<DomainRuleViolation>(() => invoice.SchedulePayment("PAY-1", DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SupplierInvoice_SchedulesPaymentOnlyAfterSuccessfulMatch()
    {
        var request = CreateApprovedRequest(quantity: 4m);
        var order = CreateIssuedOrder(request, 20m);
        var orderLine = Assert.Single(order.Lines);
        var receipt = GoodsReceipt.Record(Guid.NewGuid(), order,
            [new GoodsReceiptLine(orderLine.Id, new Quantity(4m, "EA"))],
            new Dictionary<Guid, Quantity> { [orderLine.Id] = Quantity.Zero("EA") }, DateTimeOffset.UtcNow);
        var invoice = SupplierInvoice.Create(Guid.NewGuid(), "INV-002", order,
            [new SupplierInvoiceLine(orderLine.Id, new Quantity(4m, "EA"), new Money(20m))]);
        invoice.MatchAgainst(order, [receipt], DateTimeOffset.UtcNow);

        var payment = invoice.SchedulePayment("PAY-002", DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow);

        Assert.Equal(invoice.Id, payment.InvoiceId);
        Assert.Equal(SupplierInvoiceStatus.PaymentScheduled, invoice.Status);
        Assert.Throws<DomainRuleViolation>(() => invoice.SchedulePayment("PAY-003", DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow));
    }

    private static PurchaseRequest CreateApprovedRequest(decimal quantity = 1m)
    {
        var request = PurchaseRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenantId.New(), DateTimeOffset.UtcNow);
        request.AddItem(new PurchaseRequestItem("Cement", new Quantity(quantity, "EA")));
        request.Submit();
        request.Approve("approval");
        return request;
    }

    private static PurchaseOrder CreateOrder(PurchaseRequest request, Guid supplierId, decimal price) =>
        PurchaseOrder.CreateFromApprovedRequest(Guid.NewGuid(), "PO-TEST-001", request, supplierId,
            [NewOrderLine(request.Items[0], price)]);

    private static PurchaseOrder CreateIssuedOrder(PurchaseRequest request, decimal price)
    {
        var order = CreateOrder(request, Guid.NewGuid(), price);
        var budget = ProjectBudget.Create(Guid.NewGuid(), request.TenantId, request.ProjectId, new Money(100000m));
        order.SubmitForApproval(budget);
        order.Approve("approval");
        order.Issue(DateTimeOffset.UtcNow);
        return order;
    }

    private static PurchaseOrderLine NewOrderLine(PurchaseRequestItem item, decimal price) =>
        new(item.Id, item.Description, item.Quantity, new Money(price));

    private static SupplierQuoteLine NewQuoteLine(PurchaseRequestItem item, decimal price) =>
        new(item.Id, item.Description, item.Quantity, new Money(price));
}
