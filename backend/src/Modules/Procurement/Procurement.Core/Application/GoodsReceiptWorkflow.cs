using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Core.Application.Abstractions;
using Construction.Procurement.Core.Domain.Receipts;

namespace Construction.Procurement.Core.Application;

public sealed class GoodsReceiptWorkflow(IGoodsReceiptRepository repository)
{
    public async Task<GoodsReceipt> RecordAsync(Guid orderId, IReadOnlyCollection<GoodsReceiptLine> lines,
        TenantId tenantId, DateTimeOffset receivedAt, CancellationToken cancellationToken = default)
    {
        var order = await repository.GetOrderAsync(orderId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.po_not_found");
        var receipts = await repository.GetReceiptsAsync(orderId, tenantId, cancellationToken);
        var previous = order.Lines.ToDictionary(
            line => line.Id,
            line => receipts.Where(receipt => !receipt.IsReversed)
                .SelectMany(receipt => receipt.Lines)
                .Where(receiptLine => receiptLine.PurchaseOrderLineId == line.Id)
                .Select(receiptLine => receiptLine.Quantity)
                .Aggregate(Quantity.Zero(line.Quantity.Unit), (total, quantity) => total + quantity));
        var receipt = GoodsReceipt.Record(Ids.New(), order, lines, previous, receivedAt);
        await repository.AddAsync(receipt, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return receipt;
    }

    public async Task ReverseAsync(Guid receiptId, string reason, TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var receipt = await repository.GetReceiptAsync(receiptId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.receipt_not_found");
        receipt.Reverse(reason, DateTimeOffset.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
