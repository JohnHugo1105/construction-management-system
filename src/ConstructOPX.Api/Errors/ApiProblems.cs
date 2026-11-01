using ConstructOPX.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Errors;

public static class ApiProblems
{
    public static Task WriteAsync(HttpContext context, int status, string title, string detail, string? code = null,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        ProblemDetails problem = errors is null
            ? new ProblemDetails()
            : new Microsoft.AspNetCore.Http.HttpValidationProblemDetails(errors.ToDictionary(pair => pair.Key, pair => pair.Value));
        problem.Status = status;
        problem.Title = title;
        problem.Detail = detail;

        // The exception middleware clears the response before it calls the handler, so the correlation id header is set again here.
        var traceId = context.Items[CorrelationIdMiddleware.ItemKey] as string ?? context.TraceIdentifier;
        problem.Extensions["traceId"] = traceId;
        if (code is not null) problem.Extensions["code"] = code;
        context.Response.Headers[CorrelationIdMiddleware.HeaderName] = traceId;

        context.Response.StatusCode = status;
        // Serialize by the runtime type: through the ProblemDetails base type the derived "errors" member would be left out.
        return context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json");
    }
}
