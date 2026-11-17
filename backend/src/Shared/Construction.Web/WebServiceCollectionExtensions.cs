using Construction.Web.ETag;
using Construction.Web.Errors;
using Microsoft.Extensions.DependencyInjection;

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
    /// </list>
    /// After calling this, register <c>app.UseWebExceptionHandler()</c> in the middleware pipeline.
    /// </summary>
    public static IServiceCollection AddWebInfrastructure(this IServiceCollection services)
    {
        services.AddExceptionHandler<ExceptionHandlerExtensions.ConstructionExceptionHandler>();
        services.AddProblemDetails();
        services.AddScoped<IIfMatchAccessor, HttpContextIfMatchAccessor>();
        return services;
    }
}
