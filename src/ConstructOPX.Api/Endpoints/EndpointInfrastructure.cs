using ConstructOPX.Application.Common;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// A feature's endpoints. Classes implementing this are found and mapped at start-up, so a feature adds its routes without editing
/// <c>Program.cs</c>. The group passed in is <c>/api</c>: it already requires a signed-in person, validates the anti-forgery token on
/// every write, and marks responses private and uncacheable.
/// </summary>
public interface IEndpointModule
{
    void Map(RouteGroupBuilder api);
}

/// <summary>Query-string paging, bound with <c>[AsParameters]</c>.</summary>
public sealed record PageQuery(int? Page, int? PageSize)
{
    public PageRequest ToRequest() => new(Page ?? 1, PageSize ?? PageRequest.DefaultPageSize);
}

public static class EndpointExtensions
{
    /// <summary>Requires the named permission (see <see cref="Permissions"/>).</summary>
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission) =>
        builder.RequireAuthorization(permission);

    public static RouteGroupBuilder MapFeature(this RouteGroupBuilder api, string prefix, string tag) =>
        api.MapGroup(prefix).WithTags(tag);

    public static RouteHandlerBuilder Created<T>(this RouteHandlerBuilder builder) => builder.Produces<T>(StatusCodes.Status201Created);
}

internal sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method))
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        return await next(context);
    }
}

internal sealed class PrivateResponseFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "private, no-store";
        return await next(context);
    }
}
