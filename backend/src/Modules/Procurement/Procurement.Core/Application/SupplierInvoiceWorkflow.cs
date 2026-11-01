using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Core.Application.Abstractions;
using Construction.Procurement.Core.Domain.Invoices;

namespace Construction.Procurement.Core.Application;

public sealed class SupplierInvoiceWorkflow(
    ISupplierInvoiceRepository repository,
    IProcurementEventPublisher events)
{
    public async Task<SupplierInvoice> RegisterAsync(Guid orderId, string invoiceNumber,
        IReadOnlyCollection<SupplierInvoiceLine> lines, TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var order = await repository.GetOrderAsync(orderId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.po_not_found");
        var invoice = SupplierInvoice.Create(Ids.New(), invoiceNumber, order, lines);
        await repository.AddAsync(invoice, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return invoice;
    }

    public async Task<SupplierInvoice> MatchAsync(Guid invoiceId, TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await repository.GetInvoiceAsync(invoiceId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.invoice_not_found");
        var order = await repository.GetOrderAsync(invoice.PurchaseOrderId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.po_not_found");
        var receipts = await repository.GetReceiptsAsync(order.Id, tenantId, cancellationToken);
        invoice.MatchAgainst(order, receipts, DateTimeOffset.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
        return invoice;
    }

    public async Task SchedulePaymentAsync(Guid invoiceId, string paymentReference,
        DateOnly scheduledFor, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var invoice = await repository.GetInvoiceAsync(invoiceId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.invoice_not_found");
        var @event = invoice.SchedulePayment(paymentReference, scheduledFor, DateTimeOffset.UtcNow);
        await events.PublishAsync(@event, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
