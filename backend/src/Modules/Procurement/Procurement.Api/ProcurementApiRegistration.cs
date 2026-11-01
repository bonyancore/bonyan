using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Core.Application;
using Construction.Procurement.Core.Domain.Invoices;
using Construction.Procurement.Core.Domain.PurchaseOrders;
using Construction.Procurement.Core.Domain.Receipts;
using Construction.Procurement.Core.Domain.RequestsForQuotation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Construction.Procurement.Api;

public static class ProcurementApiRegistration
{
    public static IEndpointRouteBuilder MapProcurementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/procurement").WithTags("Procurement");
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (DomainRuleViolation exception) { return Results.BadRequest(new { code = exception.Code }); }
            catch (KeyNotFoundException exception) { return Results.NotFound(new { code = exception.Message }); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "request.invalid" }); }
        });

        group.MapPost("/purchase-requests", async (CreatePurchaseRequestInput input, PurchaseRequestWorkflow workflow,
            ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Created($"/api/v1/procurement/purchase-requests", await workflow.CreateAsync(
                input.ProjectId, input.RequestedBy, tenant.Current,
                input.Items.Select(item => (item.Description, new Quantity(item.Quantity, item.Unit))), cancellationToken)));
        group.MapPost("/purchase-requests/{id:guid}/submit", async (Guid id, PurchaseRequestWorkflow workflow,
            ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Ok(await workflow.SubmitAndRequestApprovalAsync(id, tenant.Current, cancellationToken)));

        group.MapPost("/suppliers", async (CreateSupplierInput input, SupplierRegistry registry,
            ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Created($"/api/v1/procurement/suppliers", await registry.RegisterAsync(
                input.CorePartyId, tenant.Current, input.Name, input.Email, input.Phone, cancellationToken)));
        group.MapPut("/suppliers/{id:guid}/contact", async (Guid id, UpdateSupplierContactInput input,
            SupplierRegistry registry, ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Ok(await registry.UpdateContactAsync(id, tenant.Current, input.Email, input.Phone, cancellationToken)));
        group.MapPatch("/suppliers/{id:guid}/status", async (Guid id, SetSupplierStatusInput input,
            SupplierRegistry registry, ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Ok(await registry.SetActiveAsync(id, tenant.Current, input.Active, cancellationToken)));

        group.MapPost("/rfqs", async (CreateRfqInput input, RequestForQuotationWorkflow workflow,
            ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Created($"/api/v1/procurement/rfqs", await workflow.CreateAsync(input.PurchaseRequestId, tenant.Current, cancellationToken)));
        group.MapPost("/rfqs/{id:guid}/suppliers", async (Guid id, InviteSupplierInput input,
            RequestForQuotationWorkflow workflow, ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Ok(await workflow.InviteSupplierAsync(id, input.SupplierId, tenant.Current, cancellationToken)));
        group.MapPost("/rfqs/{id:guid}/issue", async (Guid id, RequestForQuotationWorkflow workflow,
            ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Ok(await workflow.IssueAsync(id, tenant.Current, cancellationToken)));
        group.MapPost("/rfqs/{id:guid}/quotes", async (Guid id, RecordQuoteInput input,
            RequestForQuotationWorkflow workflow, ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Ok(await workflow.RecordQuoteAsync(id, input.SupplierId,
                input.Lines.Select(line => new SupplierQuoteLine(line.PurchaseRequestItemId, line.Description,
                    new Quantity(line.Quantity, line.Unit), new Money(line.UnitPrice, line.Currency))).ToArray(),
                input.SubmittedAt, tenant.Current, cancellationToken)));
        group.MapGet("/rfqs/{id:guid}/comparison", async (Guid id, RequestForQuotationWorkflow workflow,
            ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Ok(await workflow.CompareAsync(id, tenant.Current, cancellationToken)));
        group.MapPost("/rfqs/{id:guid}/award", async (Guid id, AwardQuoteInput input,
            RequestForQuotationWorkflow workflow, ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Ok(await workflow.AwardAsync(id, input.SupplierId, tenant.Current, cancellationToken)));

        group.MapPost("/purchase-orders/from-request", async (CreatePurchaseOrderInput input,
            PurchaseOrderWorkflow workflow, ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Created("/api/v1/procurement/purchase-orders", await workflow.CreateFromRequestAsync(
                input.PurchaseRequestId, input.SupplierId, ToPurchaseOrderLines(input.Lines), tenant.Current, cancellationToken)));
        group.MapPut("/projects/{projectId:guid}/budget", async (Guid projectId, SetProjectBudgetInput input,
            ProjectBudgetWorkflow workflow, ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Ok(await workflow.SetLimitAsync(projectId, tenant.Current,
                new Money(input.LimitAmount, input.Currency), cancellationToken)));
        group.MapPost("/purchase-orders/from-quote", async (CreatePurchaseOrderFromQuoteInput input,
            PurchaseOrderWorkflow workflow, ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Created("/api/v1/procurement/purchase-orders", await workflow.CreateFromAwardedQuoteAsync(
                input.PurchaseRequestId, input.RequestForQuotationId, tenant.Current, cancellationToken)));
        group.MapPut("/purchase-orders/{id:guid}/lines", async (Guid id, RevisePurchaseOrderInput input,
            PurchaseOrderWorkflow workflow, ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Ok(await workflow.ReviseAsync(id, ToPurchaseOrderLines(input.Lines), tenant.Current, cancellationToken)));
        group.MapPost("/purchase-orders/{id:guid}/cancel", async (Guid id, CancelPurchaseOrderInput input,
            PurchaseOrderWorkflow workflow, ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Ok(await workflow.CancelAsync(id, input.Reason, tenant.Current, cancellationToken)));

        group.MapPost("/goods-receipts", async (RecordGoodsReceiptInput input, GoodsReceiptWorkflow workflow,
            ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Created("/api/v1/procurement/goods-receipts", await workflow.RecordAsync(
                input.PurchaseOrderId, input.Lines.Select(line => new GoodsReceiptLine(
                    line.PurchaseOrderLineId, new Quantity(line.Quantity, line.Unit))).ToArray(),
                tenant.Current, input.ReceivedAt, cancellationToken)));
        group.MapPost("/goods-receipts/{id:guid}/reverse", async (Guid id, ReverseGoodsReceiptInput input,
            GoodsReceiptWorkflow workflow, ITenantContext tenant, CancellationToken cancellationToken) =>
        {
            await workflow.ReverseAsync(id, input.Reason, tenant.Current, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/supplier-invoices", async (RegisterSupplierInvoiceInput input,
            SupplierInvoiceWorkflow workflow, ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Created("/api/v1/procurement/supplier-invoices", await workflow.RegisterAsync(
                input.PurchaseOrderId, input.InvoiceNumber, input.Lines.Select(line => new SupplierInvoiceLine(
                    line.PurchaseOrderLineId, new Quantity(line.Quantity, line.Unit),
                    new Money(line.UnitPrice, line.Currency))).ToArray(), tenant.Current, cancellationToken)));
        group.MapPost("/supplier-invoices/{id:guid}/match", async (Guid id, SupplierInvoiceWorkflow workflow,
            ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Ok(await workflow.MatchAsync(id, tenant.Current, cancellationToken)));
        group.MapPost("/supplier-invoices/{id:guid}/payments", async (Guid id, SchedulePaymentInput input,
            SupplierInvoiceWorkflow workflow, ITenantContext tenant, CancellationToken cancellationToken) =>
        {
            await workflow.SchedulePaymentAsync(id, input.PaymentReference, input.ScheduledFor,
                tenant.Current, cancellationToken);
            return Results.Accepted();
        });
        return app;
    }

    private static PurchaseOrderLine[] ToPurchaseOrderLines(IReadOnlyCollection<PurchaseOrderLineInput> lines) =>
        lines.Select(line => new PurchaseOrderLine(line.PurchaseRequestItemId, line.Description,
            new Quantity(line.Quantity, line.Unit), new Money(line.UnitPrice, line.Currency))).ToArray();

    public sealed record PurchaseRequestItemInput(string Description, decimal Quantity, string Unit);
    public sealed record CreatePurchaseRequestInput(Guid ProjectId, Guid RequestedBy, IReadOnlyList<PurchaseRequestItemInput> Items);
    public sealed record CreateSupplierInput(Guid CorePartyId, string Name, string? Email, string? Phone);
    public sealed record UpdateSupplierContactInput(string? Email, string? Phone);
    public sealed record SetSupplierStatusInput(bool Active);
    public sealed record CreateRfqInput(Guid PurchaseRequestId);
    public sealed record InviteSupplierInput(Guid SupplierId);
    public sealed record QuoteLineInput(Guid PurchaseRequestItemId, string Description, decimal Quantity, string Unit, decimal UnitPrice, string Currency);
    public sealed record RecordQuoteInput(Guid SupplierId, IReadOnlyList<QuoteLineInput> Lines, DateTimeOffset SubmittedAt);
    public sealed record AwardQuoteInput(Guid SupplierId);
    public sealed record PurchaseOrderLineInput(Guid PurchaseRequestItemId, string Description, decimal Quantity, string Unit, decimal UnitPrice, string Currency);
    public sealed record CreatePurchaseOrderInput(Guid PurchaseRequestId, Guid SupplierId, IReadOnlyCollection<PurchaseOrderLineInput> Lines);
    public sealed record CreatePurchaseOrderFromQuoteInput(Guid PurchaseRequestId, Guid RequestForQuotationId);
    public sealed record SetProjectBudgetInput(decimal LimitAmount, string Currency);
    public sealed record RevisePurchaseOrderInput(IReadOnlyCollection<PurchaseOrderLineInput> Lines);
    public sealed record CancelPurchaseOrderInput(string Reason);
    public sealed record GoodsReceiptLineInput(Guid PurchaseOrderLineId, decimal Quantity, string Unit);
    public sealed record RecordGoodsReceiptInput(Guid PurchaseOrderId, DateTimeOffset ReceivedAt, IReadOnlyCollection<GoodsReceiptLineInput> Lines);
    public sealed record ReverseGoodsReceiptInput(string Reason);
    public sealed record SupplierInvoiceLineInput(Guid PurchaseOrderLineId, decimal Quantity, string Unit, decimal UnitPrice, string Currency);
    public sealed record RegisterSupplierInvoiceInput(Guid PurchaseOrderId, string InvoiceNumber, IReadOnlyCollection<SupplierInvoiceLineInput> Lines);
    public sealed record SchedulePaymentInput(string PaymentReference, DateOnly ScheduledFor);
}
