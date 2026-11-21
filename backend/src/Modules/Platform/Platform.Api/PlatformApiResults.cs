using Construction.Kernel;
using Construction.Web.Errors;
using Microsoft.AspNetCore.Http;

namespace Construction.Platform.Api;

/// <summary>
/// Every platform endpoint error goes through here so the response is
/// <c>application/problem+json</c> with a canonical <c>platform.*</c> code (CC-03).
/// </summary>
internal static class PlatformApiResults
{
    public static IResult FromError(Error error)
    {
        var (_, problem) = error.ToProblemDetails();
        return Results.Problem(problem);
    }

    public static IResult Code(int status, string code, string detail)
    {
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = status,
            Title = code,
            Detail = detail,
            Type = "https://construction.example.com/errors/" + code.Replace('.', '-')
        };
        problem.Extensions["code"] = code;
        return Results.Problem(problem);
    }
}
