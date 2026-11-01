using Construction.Modules;
using Construction.Procurement.Api;
using Construction.Procurement.Infrastructure;
using Construction.Procurement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Procurement;

public sealed class ProcurementModule : IModule
{
    public string Code => "procurement";
    public IReadOnlyCollection<PermissionDefinition> Permissions =>
    [
        new("procurement.purchase-request.manage", "Create and submit purchase requests"),
        new("procurement.supplier.manage", "Manage procurement suppliers"),
        new("procurement.rfq.manage", "Create and compare supplier quotations"),
        new("procurement.purchase-order.manage", "Create, issue, revise, and cancel purchase orders"),
        new("procurement.project-budget.manage", "Configure local project procurement budgets"),
        new("procurement.receipt.manage", "Record and reverse goods receipts"),
        new("procurement.invoice.manage", "Register, match, and schedule supplier invoices"),
    ];
    public IReadOnlyCollection<MigrationInfo> Migrations =>
    [
        new(ProcurementDbContext.Schema, typeof(ProcurementDbContext)),
    ];

    public void AddServices(IServiceCollection services, IConfiguration config) => services.AddProcurementInfrastructure(config);

    public void MapEndpoints(IEndpointRouteBuilder app) => app.MapProcurementEndpoints();
}
