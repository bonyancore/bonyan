using Construction.Docs.Core.Application;
using Construction.Docs.Core.Application.Abstractions;
using Construction.Docs.Infrastructure.Development;
using Construction.Docs.Infrastructure.Persistence;
using Construction.Persistence;
using Construction.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Construction.Docs.Infrastructure;

public static class DocsInfrastructureRegistration
{
    public static IServiceCollection AddDocsInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddPersistenceInfrastructure();
        services.AddDbContext<DocsDbContext>((provider, options) => options
            .UseNpgsql(config.GetConnectionString("Default"), postgres =>
                postgres.MigrationsHistoryTable("__ef_migrations_history", DocsDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                provider.GetRequiredService<AuditSaveChangesInterceptor>(),
                provider.GetRequiredService<TenantSessionInterceptor>()));
        services.TryAddScoped<IDocumentRepository, DocsRepository>();
        services.TryAddScoped<DocumentService>();
        if (string.Equals(config["Docs:UseMockFileService"], "true", StringComparison.OrdinalIgnoreCase))
            services.TryAddSingleton<IFileMetadataProvider, DevelopmentFileMetadataProvider>();
        return services;
    }
}
