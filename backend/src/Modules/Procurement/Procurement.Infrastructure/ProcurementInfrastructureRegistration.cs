using Construction.Kernel.Users;
using Construction.Persistence;
using Construction.Persistence.Interceptors;
using Construction.Procurement.Infrastructure.Persistence;
using Construction.Procurement.Infrastructure.Development;
using Construction.Procurement.Core.Application.Abstractions;
using Construction.Procurement.Core.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Construction.Procurement.Infrastructure;

public static class ProcurementInfrastructureRegistration
{
    public static IServiceCollection AddProcurementInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddPersistenceInfrastructure();
        services.TryAddScoped<IUserContext, NullUserContext>();
        services.TryAddScoped<ProcurementRepository>();
        services.TryAddScoped<IPurchaseRequestRepository>(provider => provider.GetRequiredService<ProcurementRepository>());
        services.TryAddScoped<IPurchaseOrderRepository>(provider => provider.GetRequiredService<ProcurementRepository>());
        services.TryAddScoped<IProjectBudgetProvider>(provider => provider.GetRequiredService<ProcurementRepository>());
        services.TryAddScoped<IProcurementEventPublisher>(provider => provider.GetRequiredService<ProcurementRepository>());
        services.TryAddScoped<PurchaseRequestWorkflow>();
        services.TryAddScoped<PurchaseOrderWorkflow>();
        services.TryAddScoped<SitePurchaseRequestConfirmationHandler>();
        services.TryAddScoped<SupplierRegistry>();
        services.TryAddScoped<RequestForQuotationWorkflow>();
        services.TryAddScoped<GoodsReceiptWorkflow>();
        services.TryAddScoped<SupplierInvoiceWorkflow>();
        services.TryAddScoped<ProjectBudgetWorkflow>();
        services.TryAddScoped<ISupplierRegistryRepository>(provider => provider.GetRequiredService<ProcurementRepository>());
        services.TryAddScoped<IRequestForQuotationRepository>(provider => provider.GetRequiredService<ProcurementRepository>());
        services.TryAddScoped<IGoodsReceiptRepository>(provider => provider.GetRequiredService<ProcurementRepository>());
        services.TryAddScoped<ISupplierInvoiceRepository>(provider => provider.GetRequiredService<ProcurementRepository>());
        services.TryAddScoped<IProjectBudgetAdminRepository>(provider => provider.GetRequiredService<ProcurementRepository>());

        if (string.Equals(config["Procurement:UseMockPlatformServices"], "true", StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<DevelopmentProcurementNumberGenerator>();
            services.TryAddSingleton<IPurchaseRequestNumberGenerator>(provider => provider.GetRequiredService<DevelopmentProcurementNumberGenerator>());
            services.TryAddSingleton<IPurchaseOrderNumberGenerator>(provider => provider.GetRequiredService<DevelopmentProcurementNumberGenerator>());
            services.TryAddSingleton<IApprovalEngine, DevelopmentApprovalEngine>();
        }

        services.AddDbContext<ProcurementDbContext>((serviceProvider, options) => options
            .UseNpgsql(
                config.GetConnectionString("Default"),
                postgres => postgres.MigrationsHistoryTable("__ef_migrations_history", ProcurementDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
                serviceProvider.GetRequiredService<TenantSessionInterceptor>()));

        return services;
    }
}
