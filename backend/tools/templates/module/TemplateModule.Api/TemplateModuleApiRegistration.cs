using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Construction.TemplateModule.Api;

public static class TemplateModuleApiRegistration
{
    public static IEndpointRouteBuilder MapTemplateModuleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/templatemodule")
            .WithTags("TemplateModule");

        return app;
    }
}

