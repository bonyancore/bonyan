namespace Construction.Procurement.Core.Domain.RequestsForQuotation;

public sealed record SupplierQuoteComparison(
    Guid SupplierId,
    string Currency,
    decimal TotalAmount,
    DateTimeOffset SubmittedAt,
    IReadOnlyList<SupplierQuoteComparisonLine> Lines);

public sealed record SupplierQuoteComparisonLine(
    Guid PurchaseRequestItemId,
    string Description,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal LineTotal);
