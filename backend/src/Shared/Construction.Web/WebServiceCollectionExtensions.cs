using Construction.Web.ETag;
using Construction.Web.Errors;
using Construction.Web.Idempotency;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Construction.Web;

/// <summary>
/// DI registration for <c>Construction.Web</c> infrastructure.
/// </summary>
public static class WebServiceCollectionExtensions
{
    /// <summary>
    /// Registers Construction.Web services:
    /// <list type="bullet">
    ///   <item>RFC 7807 exception handler (<see cref="ExceptionHandlerExtensions.ConstructionExceptionHandler"/>)</item>
    ///   <item><see cref="IIfMatchAccessor"/> for optimistic concurrency precondition access</item>
    ///   <item><see cref="IdempotencyOptions"/> for the Idempotency-Key middleware (PLT-205)</item>
    /// </list>
    /// After calling this, register <c>app.UseWebExceptionHandler()</c> and
    /// <c>app.UseMiddleware&lt;IdempotencyMiddleware&gt;()</c> in the middleware pipeline.
    /// </summary>
    public static IServiceCollection AddWebInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddExceptionHandler<ExceptionHandlerExtensions.ConstructionExceptionHandler>();
        services.AddProblemDetails();
        services.AddScoped<IIfMatchAccessor, HttpContextIfMatchAccessor>();
        services.TryAddSingleton(IdempotencyOptions.FromConfiguration(configuration));
        return services;
    }
}
