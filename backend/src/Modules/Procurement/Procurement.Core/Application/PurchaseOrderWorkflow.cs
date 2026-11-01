using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Contracts.Events;
using Construction.Procurement.Core.Application.Abstractions;
using Construction.Procurement.Core.Domain.PurchaseOrders;

namespace Construction.Procurement.Core.Application;

public sealed class PurchaseOrderWorkflow(
    IPurchaseOrderRepository repository,
    IPurchaseOrderNumberGenerator numberGenerator,
    IProjectBudgetProvider budgets,
    IApprovalEngine approvals,
    IProcurementEventPublisher events)
{
    public async Task<PurchaseOrder> CreateFromRequestAsync(Guid requestId, Guid supplierId,
        IReadOnlyCollection<PurchaseOrderLine> lines, TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var request = await repository.GetPurchaseRequestAsync(requestId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.purchase_request_not_found");
        var number = await numberGenerator.GenerateAsync(tenantId, request.ProjectId, cancellationToken);
        var order = PurchaseOrder.CreateFromApprovedRequest(Ids.New(), number, request, supplierId, lines);
        await repository.AddPurchaseOrderAsync(order, cancellationToken);
        return await SubmitApproveIssueAsync(order, cancellationToken);
    }

    public async Task<PurchaseOrder> CreateFromAwardedQuoteAsync(Guid requestId, Guid rfqId,
        TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var request = await repository.GetPurchaseRequestAsync(requestId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.purchase_request_not_found");
        var rfq = await repository.GetRequestForQuotationAsync(rfqId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.rfq_not_found");
        var quote = rfq.Quotes.SingleOrDefault(item => item.SupplierId == rfq.AwardedSupplierId)
            ?? throw new DomainRuleViolation("procurement.rfq_awarded_quote_not_found");
        var number = await numberGenerator.GenerateAsync(tenantId, request.ProjectId, cancellationToken);
        var order = PurchaseOrder.CreateFromAwardedQuote(Ids.New(), number, request, rfq, quote);
        await repository.AddPurchaseOrderAsync(order, cancellationToken);
        return await SubmitApproveIssueAsync(order, cancellationToken);
    }

    public async Task<PurchaseOrder> ReviseAsync(Guid orderId, IReadOnlyCollection<PurchaseOrderLine> lines,
        TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var order = await repository.GetPurchaseOrderAsync(orderId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.po_not_found");
        var budget = await budgets.GetBudgetAsync(order.ProjectId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.project_budget_not_found");
        order.ReleaseBudgetReservation(budget);
        order.ReplaceLines(lines);
        await repository.SaveChangesAsync(cancellationToken);
        return await SubmitApproveIssueAsync(order, cancellationToken);
    }

    public async Task<PurchaseOrderCancelledV1> CancelAsync(Guid orderId, string reason,
        TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var order = await repository.GetPurchaseOrderAsync(orderId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.po_not_found");
        var budget = await budgets.GetBudgetAsync(order.ProjectId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.project_budget_not_found");
        var @event = order.Cancel(reason, DateTimeOffset.UtcNow);
        order.ReleaseBudgetReservation(budget);
        await events.PublishAsync(@event, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return @event;
    }

    private async Task<PurchaseOrder> SubmitApproveIssueAsync(PurchaseOrder order,
        CancellationToken cancellationToken)
    {
        var budget = await budgets.GetBudgetAsync(order.ProjectId, order.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.project_budget_not_found");
        order.SubmitForApproval(budget);
        await repository.SaveChangesAsync(cancellationToken);
        var decision = await approvals.RequestApprovalAsync(new ApprovalRequest(
            order.Id, "purchase-order", order.Number, order.ProjectId, order.TenantId,
            order.TotalAmount, order.Currency), cancellationToken);
        if (!decision.IsApproved)
        {
            order.ReleaseBudgetReservation(budget);
            order.Reject(decision.RejectionReason ?? decision.Reference);
            await repository.SaveChangesAsync(cancellationToken);
            return order;
        }

        order.Approve(decision.Reference);
        var issueEvent = order.Issue(DateTimeOffset.UtcNow);
        if (order.Revision == 1)
            await events.PublishAsync(issueEvent, cancellationToken);
        else
            await events.PublishAsync(order.GetModifiedEvent(DateTimeOffset.UtcNow), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return order;
    }
}
