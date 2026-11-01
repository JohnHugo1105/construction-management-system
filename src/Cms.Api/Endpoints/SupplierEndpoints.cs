using Cms.Application.Common;
using Cms.Application.Features.Suppliers;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Endpoints;

public sealed class SupplierEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var group = api.MapFeature("/suppliers", "Suppliers");

        group.MapGet("", async ([FromQuery] string? search, [FromQuery] string? kind, [FromQuery] bool? includeArchived, [AsParameters] PageQuery page,
                SupplierService suppliers, CancellationToken ct) =>
            Results.Ok(await suppliers.ListAsync(new SupplierListQuery(search, kind, includeArchived ?? false), page.ToRequest(), ct)))
            .RequirePermission(Permissions.SuppliersRead);

        group.MapGet("/lookup", async ([FromQuery] string? kind, SupplierService suppliers, CancellationToken ct) =>
            Results.Ok(await suppliers.LookupAsync(kind, ct))).RequirePermission(Permissions.SuppliersRead);

        group.MapGet("/{id:guid}", async (Guid id, SupplierService suppliers, CancellationToken ct) => Results.Ok(await suppliers.GetAsync(id, ct)))
            .RequirePermission(Permissions.SuppliersRead);

        group.MapPost("", async (SaveSupplierRequest request, SupplierService suppliers, CancellationToken ct) =>
        {
            var created = await suppliers.CreateAsync(request, ct);
            return Results.Created($"/api/suppliers/{created.Id}", created);
        }).RequirePermission(Permissions.SuppliersManage);

        group.MapPut("/{id:guid}", async (Guid id, SaveSupplierRequest request, SupplierService suppliers, CancellationToken ct) =>
            Results.Ok(await suppliers.UpdateAsync(id, request, ct))).RequirePermission(Permissions.SuppliersManage);

        group.MapPost("/{id:guid}/archive", async (Guid id, VersionRequest request, SupplierService suppliers, CancellationToken ct) =>
            Results.Ok(await suppliers.ArchiveAsync(id, request, ct))).RequirePermission(Permissions.SuppliersManage);

        group.MapPost("/{id:guid}/restore", async (Guid id, VersionRequest request, SupplierService suppliers, CancellationToken ct) =>
            Results.Ok(await suppliers.RestoreAsync(id, request, ct))).RequirePermission(Permissions.SuppliersManage);
    }
}
