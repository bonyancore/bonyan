using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Core.Domain.PurchaseOrders;
using Construction.Procurement.Core.Domain.Receipts;
using Construction.Procurement.Contracts.Events;

namespace Construction.Procurement.Core.Domain.Invoices;

public sealed class SupplierInvoice : ITenantScopedEntity, IAuditableEntity, IConcurrencyVersioned
{
    private readonly List<SupplierInvoiceLine> _lines = [];
    private SupplierInvoice() { }

    private SupplierInvoice(Guid id, TenantId tenantId, Guid purchaseOrderId, Guid supplierId, string invoiceNumber)
    {
        Id = id;
        TenantId = tenantId;
        PurchaseOrderId = purchaseOrderId;
        SupplierId = supplierId;
        InvoiceNumber = invoiceNumber;
        Status = SupplierInvoiceStatus.Draft;
    }

    public Guid Id { get; private set; }
    public TenantId TenantId { get; set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;
    public SupplierInvoiceStatus Status { get; private set; }
    public string? DisputeReason { get; private set; }
    public IReadOnlyList<SupplierInvoiceLine> Lines => _lines.AsReadOnly();
    public decimal TotalAmount => _lines.Sum(line => line.Total.Amount);
    public string Currency => _lines.FirstOrDefault()?.Currency ?? Money.DefaultCurrency;
    public DateTime? SubmittedAt { get; private set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public uint RowVersion { get; set; }

    public static SupplierInvoice Create(Guid id, string invoiceNumber, PurchaseOrder order,
        IReadOnlyCollection<SupplierInvoiceLine> lines)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(lines);
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(invoiceNumber) || invoiceNumber.Trim().Length > 120)
            throw new DomainRuleViolation("procurement.invoice_identity_required");
        if (order.Status != PurchaseOrderStatus.Issued || lines.Count == 0)
            throw new DomainRuleViolation("procurement.invoice_order_or_lines_invalid");
        if (lines.Select(line => line.PurchaseOrderLineId).Distinct().Count() != lines.Count ||
            !order.Lines.Select(line => line.Id).ToHashSet().IsSupersetOf(lines.Select(line => line.PurchaseOrderLineId)))
            throw new DomainRuleViolation("procurement.invoice_lines_invalid");

        var invoice = new SupplierInvoice(id, order.TenantId, order.Id, order.SupplierId, invoiceNumber.Trim());
        foreach (var line in lines)
        {
            var orderLine = order.Lines.Single(item => item.Id == line.PurchaseOrderLineId);
            if (line.Currency != orderLine.Currency || line.Quantity.Unit != orderLine.Quantity.Unit)
                throw new DomainRuleViolation("procurement.invoice_line_mismatch");
            line.Attach(invoice.Id, invoice.TenantId);
            invoice._lines.Add(line);
        }
        return invoice;
    }

    public void MatchAgainst(PurchaseOrder order, IReadOnlyCollection<GoodsReceipt> receipts, DateTimeOffset submittedAt)
    {
        if (Status != SupplierInvoiceStatus.Draft)
            throw new DomainRuleViolation("procurement.invoice_already_submitted");
        if (order.Id != PurchaseOrderId || order.TenantId != TenantId || submittedAt == default)
            throw new DomainRuleViolation("procurement.invoice_match_context_invalid");

        var reasons = new List<string>();
        foreach (var invoiceLine in _lines)
        {
            var orderLine = order.Lines.Single(line => line.Id == invoiceLine.PurchaseOrderLineId);
            var received = receipts.Where(receipt => receipt.PurchaseOrderId == order.Id && !receipt.IsReversed)
                .SelectMany(receipt => receipt.Lines)
                .Where(line => line.PurchaseOrderLineId == orderLine.Id)
                .Sum(line => line.Quantity.Amount);
            if (invoiceLine.Quantity.Amount > received)
                reasons.Add($"{invoiceLine.PurchaseOrderLineId}: invoice quantity exceeds received quantity");
            if (invoiceLine.UnitPriceAmount != orderLine.UnitPriceAmount)
                reasons.Add($"{invoiceLine.PurchaseOrderLineId}: invoice price differs from purchase order");
        }

        SubmittedAt = submittedAt.UtcDateTime;
        if (reasons.Count == 0)
        {
            Status = SupplierInvoiceStatus.Matched;
            DisputeReason = null;
        }
        else
        {
            Status = SupplierInvoiceStatus.Disputed;
            var reason = string.Join("; ", reasons);
            DisputeReason = reason[..Math.Min(reason.Length, 2000)];
        }
    }

    public SupplierPaymentScheduledV1 SchedulePayment(
        string paymentReference,
        DateOnly scheduledFor,
        DateTimeOffset scheduledAt)
    {
        if (Status != SupplierInvoiceStatus.Matched)
            throw new DomainRuleViolation("procurement.payment_invoice_not_matched");
        if (string.IsNullOrWhiteSpace(paymentReference) || scheduledAt == default ||
            scheduledFor < DateOnly.FromDateTime(scheduledAt.UtcDateTime))
            throw new DomainRuleViolation("procurement.payment_schedule_details_invalid");
        Status = SupplierInvoiceStatus.PaymentScheduled;
        return new SupplierPaymentScheduledV1(
            Ids.New(), TenantId.Value, Id, PurchaseOrderId, SupplierId, paymentReference.Trim(),
            TotalAmount, Currency, scheduledFor, scheduledAt.ToUniversalTime());
    }
}

public enum SupplierInvoiceStatus { Draft, Matched, Disputed, PaymentScheduled, Paid }

public sealed class SupplierInvoiceLine : ITenantScopedEntity, IAuditableEntity
{
    private decimal _quantityAmount;
    private string _quantityUnit = string.Empty;
    private SupplierInvoiceLine() { }

    public SupplierInvoiceLine(Guid purchaseOrderLineId, Quantity quantity, Money unitPrice)
    {
        if (purchaseOrderLineId == Guid.Empty || quantity.Amount <= 0 || unitPrice.Amount < 0)
            throw new DomainRuleViolation("procurement.invoice_line_invalid");
        Id = Ids.New();
        PurchaseOrderLineId = purchaseOrderLineId;
        _quantityAmount = quantity.Amount;
        _quantityUnit = quantity.Unit;
        UnitPriceAmount = unitPrice.Amount;
        Currency = unitPrice.Currency;
    }

    public Guid Id { get; private set; }
    public Guid SupplierInvoiceId { get; private set; }
    public Guid PurchaseOrderLineId { get; private set; }
    public TenantId TenantId { get; set; }
    public Quantity Quantity => new(_quantityAmount, _quantityUnit);
    public decimal QuantityAmount { get => _quantityAmount; private set => _quantityAmount = value; }
    public string QuantityUnit { get => _quantityUnit; private set => _quantityUnit = value; }
    public decimal UnitPriceAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public Money Total => new(UnitPriceAmount * QuantityAmount, Currency);
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    internal void Attach(Guid invoiceId, TenantId tenantId)
    {
        if (SupplierInvoiceId != Guid.Empty || invoiceId == Guid.Empty)
            throw new DomainRuleViolation("procurement.invoice_line_attach_invalid");
        SupplierInvoiceId = invoiceId;
        TenantId = tenantId;
    }
}
